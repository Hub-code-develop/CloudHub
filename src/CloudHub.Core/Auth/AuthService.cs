using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CloudHub.Core.Auth;

/// <summary>
/// 手写的 OAuth2 设备代码流 + 刷新令牌，不依赖 MSAL。
/// 好处：依赖为零、流程完全可见、任何公开客户端都能用。
/// </summary>
public sealed class AuthService : IDisposable
{
    private readonly HttpClient _http;
    private readonly TokenStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AuthService(AuthOptions options, TokenStore? store = null, HttpMessageHandler? handler = null)
    {
        Options = options;
        _store = store ?? new TokenStore();
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(100);
    }

    public AuthOptions Options { get; private set; }

    public TokenSet? Tokens { get; private set; }

    public UserProfile? CurrentUser { get; private set; }

    public bool IsSignedIn => Tokens is not null && (!Tokens.IsExpired() || !string.IsNullOrEmpty(Tokens.RefreshToken));

    public event EventHandler<TokenSet>? TokensChanged;
    public event EventHandler? SignedOut;

    /// <summary>换掉配置（用户改了租户 / client id）并清空当前身份。</summary>
    public void Reconfigure(AuthOptions options)
    {
        Options = options;
        Tokens = null;
        CurrentUser = null;
        _store.Clear();
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------------------------------------- 设备代码流

    public async Task<DeviceCodeChallenge> BeginDeviceCodeFlowAsync(CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = Options.ClientId,
            ["scope"] = Options.BuildScope(),
        });

        using var response = await _http.PostAsync(Options.DeviceCodeEndpoint, content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw ParseError(body, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var e) ? e : 900;
        var interval = root.TryGetProperty("interval", out var iv) && iv.TryGetInt32(out var i) ? i : 5;

        return new DeviceCodeChallenge
        {
            DeviceCode = root.GetProperty("device_code").GetString() ?? string.Empty,
            UserCode = root.GetProperty("user_code").GetString() ?? string.Empty,
            VerificationUri = root.TryGetProperty("verification_uri", out var vu) ? vu.GetString() ?? string.Empty : string.Empty,
            VerificationUriComplete = root.TryGetProperty("verification_uri_complete", out var vuc) ? vuc.GetString() : null,
            ExpiresIn = expiresIn,
            Interval = Math.Max(interval, 1),
            Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
        };
    }

    /// <summary>
    /// 按 interval 轮询直到拿到令牌。失败会抛 <see cref="CloudHubAuthException"/>，
    /// 但 authorization_pending / slow_down 属于正常等待，不会抛出。
    /// </summary>
    public async Task<TokenSet> PollForTokenAsync(DeviceCodeChallenge challenge, CancellationToken ct = default)
    {
        var interval = TimeSpan.FromSeconds(challenge.Interval);

        while (DateTimeOffset.UtcNow < challenge.ExpiresAt)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(interval, ct).ConfigureAwait(false);

            try
            {
                var tokens = await RedeemDeviceCodeAsync(challenge.DeviceCode, ct).ConfigureAwait(false);
                await PublishAsync(tokens, ct).ConfigureAwait(false);
                return tokens;
            }
            catch (CloudHubAuthException ex) when (ex.Error == "authorization_pending")
            {
                // 用户还没在浏览器里点确认，继续等。
            }
            catch (CloudHubAuthException ex) when (ex.Error == "slow_down")
            {
                // 服务端要求降速：官方建议步长 +5 秒。
                interval += TimeSpan.FromSeconds(5);
            }
        }

        throw new CloudHubAuthException("expired_token", "设备代码已过期，请重新发起登录。");
    }

    private async Task<TokenSet> RedeemDeviceCodeAsync(string deviceCode, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["client_id"] = Options.ClientId,
            ["device_code"] = deviceCode,
        });

        using var response = await _http.PostAsync(Options.TokenEndpoint, content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw ParseError(body, response.StatusCode);
        }

        return ReadTokenResponse(body);
    }

    // ---------------------------------------------------------------- 刷新 / 取令牌

    /// <summary>拿一个可用的 access token，必要时自动用 refresh token 续期。</summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var tokens = Tokens ?? throw new CloudHubAuthException("no_account", "尚未登录。");

        if (!tokens.IsExpired() && !string.IsNullOrEmpty(tokens.AccessToken))
        {
            return tokens.AccessToken;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 双重检查：可能已被并发的另一个调用刷新过了。
            if (!tokens.IsExpired() && !string.IsNullOrEmpty(tokens.AccessToken))
            {
                return tokens.AccessToken;
            }

            if (string.IsNullOrEmpty(tokens.RefreshToken))
            {
                throw new CloudHubAuthException("no_refresh_token", "会话已过期且没有刷新令牌，请重新登录。");
            }

            var refreshed = await RefreshAsync(tokens.RefreshToken, ct).ConfigureAwait(false);
            await PublishAsync(refreshed, ct).ConfigureAwait(false);
            return refreshed.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>用 refresh token 换新令牌。</summary>
    public async Task<TokenSet> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = Options.ClientId,
            ["refresh_token"] = refreshToken,
            ["scope"] = Options.BuildScope(),
        });

        using var response = await _http.PostAsync(Options.TokenEndpoint, content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = ParseError(body, response.StatusCode);
            if (error.Error is "invalid_grant" or "interaction_required" or "invalid_client")
            {
                SignOut();
            }

            throw error;
        }

        var tokens = ReadTokenResponse(body);
        // 刷新响应里 refresh_token 可能不返回，沿用旧的（Entra 的 rolling refresh 行为）。
        tokens.RefreshToken ??= refreshToken;
        return tokens;
    }

    /// <summary>
    /// 用已持有的 refresh token，向指定租户的令牌端点换取一张 ARM access token。
    /// 这是「逐租户枚举订阅」的关键：多租户 refresh token 可在任意租户端点兑换，
    /// 从而拿到能在该租户下调用 /subscriptions 的令牌（覆盖个人 MSA / 跨目录订阅）。
    /// 该方法不会改写主会话令牌，避免把家庭租户的 refresh token 覆盖成其他租户的。
    /// </summary>
    public async Task<string> GetAccessTokenForTenantAsync(string tenantId, CancellationToken ct = default)
    {
        var tokens = Tokens ?? throw new CloudHubAuthException("no_account", "尚未登录。");
        if (string.IsNullOrEmpty(tokens.RefreshToken))
        {
            throw new CloudHubAuthException("no_refresh_token", "没有刷新令牌，无法枚举其他租户的订阅。");
        }

        var endpoint = $"{Options.AuthorityHost.TrimEnd('/')}/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = Options.ClientId,
            ["refresh_token"] = tokens.RefreshToken,
            ["scope"] = Options.BuildScope(),
        });

        using var response = await _http.PostAsync(endpoint, content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw ParseError(body, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()
               ?? throw new CloudHubAuthException("no_token", "租户令牌响应缺少 access_token。");
    }

    /// <summary>启动时用磁盘上的缓存恢复会话。</summary>
    public async Task<bool> TryRestoreAsync(CancellationToken ct = default)
    {
        var cached = await _store.LoadAsync(ct).ConfigureAwait(false);

        // client id 换过了，旧令牌不能再用。
        if (cached is null || cached.ClientId != Options.ClientId)
        {
            return false;
        }

        // refresh token 没了且 access token 也过期，就得重新登录。
        if (string.IsNullOrEmpty(cached.RefreshToken) && cached.IsExpired())
        {
            return false;
        }

        Tokens = cached;
        CurrentUser = JwtInspector.DescribeUser(cached.IdToken);
        TokensChanged?.Invoke(this, cached);

        try
        {
            // 主动刷一次：既确认 refresh token 还有效，也保证后续请求拿的是新令牌。
            if (cached.IsExpired() || string.IsNullOrEmpty(cached.AccessToken))
            {
                var refreshed = await RefreshAsync(cached.RefreshToken!, ct).ConfigureAwait(false);
                await PublishAsync(refreshed, ct).ConfigureAwait(false);
            }

            return true;
        }
        catch (CloudHubAuthException)
        {
            return false;
        }
    }

    public void SignOut()
    {
        Tokens = null;
        CurrentUser = null;
        _store.Clear();
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------------------------------------- 内部

    private async Task PublishAsync(TokenSet tokens, CancellationToken ct)
    {
        tokens.ClientId = Options.ClientId;
        tokens.TenantId = JwtInspector.ReadTenantId(tokens.AccessToken) ?? Options.TenantId;

        Tokens = tokens;
        CurrentUser = JwtInspector.DescribeUser(tokens.IdToken) ?? CurrentUser;

        await _store.SaveAsync(tokens, ct).ConfigureAwait(false);
        TokensChanged?.Invoke(this, tokens);
    }

    private static TokenSet ReadTokenResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var e) ? e : 3600;

        return new TokenSet
        {
            AccessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() ?? string.Empty : string.Empty,
            RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            IdToken = root.TryGetProperty("id_token", out var it) ? it.GetString() : null,
            Scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() ?? string.Empty : string.Empty,
            ExpiresOn = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            AcquiredOn = DateTimeOffset.UtcNow,
        };
    }

    private static CloudHubAuthException ParseError(string body, HttpStatusCode status)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var error = root.TryGetProperty("error", out var err)
                ? err.ValueKind == JsonValueKind.String ? err.GetString() : err.GetProperty("code").GetString()
                : null;
            var description = root.TryGetProperty("error_description", out var desc) ? desc.GetString() : null;
            return new CloudHubAuthException(error ?? $"http_{(int)status}", description, body);
        }
        catch (JsonException)
        {
            return new CloudHubAuthException($"http_{(int)status}", body, body);
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}
