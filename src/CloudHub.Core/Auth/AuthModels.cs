namespace CloudHub.Core.Auth;

/// <summary>设备代码流第一阶段返回给用户的提示信息（让用户去浏览器里输码）。</summary>
public sealed record DeviceCodeChallenge
{
    public required string DeviceCode { get; init; }
    public required string UserCode { get; init; }
    public required string VerificationUri { get; init; }
    public string? VerificationUriComplete { get; init; }
    public int ExpiresIn { get; init; } = 900;
    public int Interval { get; init; } = 5;
    public string Message { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddSeconds(900);
}

/// <summary>一次成功登录得到的令牌集合。</summary>
public sealed class TokenSet
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public string? IdToken { get; set; }
    public DateTimeOffset ExpiresOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset AcquiredOn { get; set; } = DateTimeOffset.UtcNow;
    public string Scope { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;

    /// <summary>提前 5 分钟视为过期，避免请求刚好卡在边界上失败。</summary>
    public bool IsExpired(int skewSeconds = 300) =>
        DateTimeOffset.UtcNow >= ExpiresOn.AddSeconds(-skewSeconds);

    public TimeSpan Remaining =>
        ExpiresOn - DateTimeOffset.UtcNow is var span && span > TimeSpan.Zero ? span : TimeSpan.Zero;
}

/// <summary>从 id_token 解析出来的当前登录用户。</summary>
public sealed record UserProfile
{
    public string? DisplayName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? ObjectId { get; init; }
    public string? TenantId { get; init; }

    public string Initial
    {
        get
        {
            var source = DisplayName ?? UserPrincipalName ?? "?";
            return source.Length > 0 ? source[..1].ToUpperInvariant() : "?";
        }
    }

    public string Description => DisplayName ?? UserPrincipalName ?? "未知用户";
}

/// <summary>Entra ID / ARM 返回的可读错误。</summary>
public sealed class CloudHubAuthException : Exception
{
    public CloudHubAuthException(string error, string? description, string? rawBody = null)
        : base(string.IsNullOrWhiteSpace(description) ? error : $"{error}: {description}")
    {
        Error = error;
        Description = description;
        RawBody = rawBody;
    }

    public string Error { get; }
    public string? Description { get; }
    public string? RawBody { get; }

    /// <summary>
    /// 是否属于「继续等待用户操作」的正常轮询状态，而不是真失败。
    /// </summary>
    public bool IsPending => Error is "authorization_pending" or "slow_down";
}
