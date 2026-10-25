using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CloudHub.Core.Arm;

/// <summary>ARM / Azure REST 返回的结构化错误。</summary>
public sealed class ArmException : Exception
{
    public ArmException(int statusCode, string? code, string? message, string? requestId = null, string? rawBody = null)
        : base(BuildMessage(statusCode, code, message, requestId))
    {
        StatusCode = statusCode;
        Code = code;
        RequestId = requestId;
        RawBody = rawBody;
    }

    public int StatusCode { get; }
    public string? Code { get; }
    public string? RequestId { get; }
    public string? RawBody { get; }

    private static string BuildMessage(int status, string? code, string? message, string? requestId)
    {
        var text = new StringBuilder();
        text.Append("HTTP ").Append(status);
        if (!string.IsNullOrWhiteSpace(code))
        {
            text.Append(" · ").Append(code);
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            text.Append(" — ").Append(message);
        }

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            text.Append(" (requestId: ").Append(requestId).Append(')');
        }

        return text.ToString();
    }
}

/// <summary>一次 ARM 调用的原始结果，含长时操作所需的 header。</summary>
public sealed record ArmResponse(
    int StatusCode,
    JsonElement? Body,
    string? AsyncOperationUrl,
    string? LocationUrl,
    string? RequestId)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}

/// <summary>
/// Azure Resource Manager 的薄封装：自动挂 Bearer、解析错误、跟随 nextLink 翻页、针对长时操作轮询。
/// 所有路径都支持相对路径（/subscriptions/...）或完整的 nextLink / operation URL。
/// </summary>
public sealed class ArmClient : IDisposable
{
    public const string PublicArmEndpoint = "https://management.azure.com";
    public const string ChinaArmEndpoint = "https://management.chinacloudapi.cn";

    private readonly HttpClient _http;
    private readonly Func<CancellationToken, Task<string>> _accessTokenProvider;

    public ArmClient(Func<CancellationToken, Task<string>> accessTokenProvider, string? endpoint = null, HttpMessageHandler? handler = null)
    {
        _accessTokenProvider = accessTokenProvider;
        Endpoint = (endpoint ?? PublicArmEndpoint).TrimEnd('/');
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    public string Endpoint { get; }

    public string SubscriptionScope(string subscriptionId) => $"/subscriptions/{subscriptionId}";

    public string ResourceGroupScope(string subscriptionId, string resourceGroupName) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}";

    // ---------------------------------------------------------------- 基础调用

    public async Task<ArmResponse> SendAsync(
        HttpMethod method,
        string pathOrUrl,
        object? body = null,
        CancellationToken ct = default,
        string? accessToken = null)
    {
        var uri = BuildUri(pathOrUrl);
        const int maxAttempts = 4;

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", accessToken ?? await _accessTokenProvider(ct).ConfigureAwait(false));
            request.Headers.Accept.ParseAdd("application/json");

            if (body is not null)
            {
                var json = body as string ?? JsonSerializer.Serialize(body, JsonDefaults.Options);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // 限流 / 瞬时故障：退避重试。
            if ((status == 429 || status >= 500) && attempt < maxAttempts)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                await Task.Delay(delay, ct).ConfigureAwait(false);
                continue;
            }

            var requestId = response.Headers.TryGetValues("x-ms-request-id", out var ids)
                ? ids.FirstOrDefault()
                : null;

            string? asyncUrl = null;
            string? locationUrl = null;
            if (status == 202 || status == 201)
            {
                if (response.Headers.TryGetValues("Azure-AsyncOperation", out var aao))
                {
                    asyncUrl = aao.FirstOrDefault();
                }

                locationUrl = response.Headers.Location?.ToString();
            }

            if (status >= 400)
            {
                var (code, message) = ParseArmError(payload);
                throw new ArmException(status, code, message, requestId, payload);
            }

            JsonElement? element = null;
            if (!string.IsNullOrWhiteSpace(payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    element = doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    // 有些接口（例如 202/204）会返回空体或非 JSON，忽略。
                }
            }

            return new ArmResponse(status, element, asyncUrl, locationUrl, requestId);
        }
    }

    public Task<ArmResponse> GetAsync(string pathOrUrl, CancellationToken ct = default, string? accessToken = null) =>
        SendAsync(HttpMethod.Get, pathOrUrl, null, ct, accessToken);

    public Task<ArmResponse> PostAsync(string pathOrUrl, object? body = null, CancellationToken ct = default, string? accessToken = null) =>
        SendAsync(HttpMethod.Post, pathOrUrl, body, ct, accessToken);

    public Task<ArmResponse> PutAsync(string pathOrUrl, object? body = null, CancellationToken ct = default, string? accessToken = null) =>
        SendAsync(HttpMethod.Put, pathOrUrl, body, ct, accessToken);

    public Task<ArmResponse> PatchAsync(string pathOrUrl, object? body = null, CancellationToken ct = default, string? accessToken = null) =>
        SendAsync(HttpMethod.Patch, pathOrUrl, body, ct, accessToken);

    public Task<ArmResponse> DeleteAsync(string pathOrUrl, CancellationToken ct = default, string? accessToken = null) =>
        SendAsync(HttpMethod.Delete, pathOrUrl, null, ct, accessToken);

    /// <summary>调用并返回 <c>value</c> 数组（单页）。</summary>
    public async Task<IReadOnlyList<JsonElement>> GetListAsync(string pathOrUrl, CancellationToken ct = default, string? accessToken = null)
    {
        var response = await GetAsync(pathOrUrl, ct, accessToken).ConfigureAwait(false);
        return ExtractItems(response.Body);
    }

    /// <summary>调用并跟随 nextLink 把所有页拉完。</summary>
    public async IAsyncEnumerable<JsonElement> GetPagedAsync(
        string pathOrUrl,
        CancellationToken ct = default,
        string? accessToken = null)
    {
        var next = pathOrUrl;
        var guard = 0;

        while (!string.IsNullOrEmpty(next))
        {
            // 防御性上限：防止服务端 nextLink 自环导致无限循环。
            if (++guard > 500)
            {
                yield break;
            }

            var response = await GetAsync(next, ct, accessToken).ConfigureAwait(false);
            foreach (var item in ExtractItems(response.Body))
            {
                yield return item;
            }

            next = response.Body is { } body
                   && body.TryGetProperty("nextLink", out var nl)
                   && nl.ValueKind == JsonValueKind.String
                ? nl.GetString()
                : null;
        }
    }

    /// <summary>把所有页拉完并返回列表。</summary>
    public async Task<IReadOnlyList<JsonElement>> GetAllPagedAsync(string pathOrUrl, CancellationToken ct = default, string? accessToken = null)
    {
        var results = new List<JsonElement>();
        await foreach (var item in GetPagedAsync(pathOrUrl, ct, accessToken).ConfigureAwait(false))
        {
            results.Add(item);
        }

        return results;
    }

    // ---------------------------------------------------------------- 长时操作

    /// <summary>等待一个异步操作完成（202 响应）。返回最终结果体（可能为空）。</summary>
    public async Task<JsonElement?> WaitForOperationAsync(ArmResponse response, CancellationToken ct = default)
    {
        if (response.StatusCode != 202)
        {
            return response.Body;
        }

        var pollUrl = response.AsyncOperationUrl ?? response.LocationUrl;
        if (string.IsNullOrEmpty(pollUrl))
        {
            return response.Body;
        }

        var delay = TimeSpan.FromSeconds(2);

        for (var i = 0; i < 300; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(delay, ct).ConfigureAwait(false);

            var poll = await GetAsync(pollUrl, ct).ConfigureAwait(false);
            var body = poll.Body;

            var status = body is { } b && b.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;

            switch (status?.ToLowerInvariant())
            {
                case null:
                    // Location 轮询：200 即完成。
                    if (poll.StatusCode is >= 200 and < 300 && response.AsyncOperationUrl is null)
                    {
                        return body;
                    }

                    break;
                case "succeeded":
                    return body;
                case "failed":
                case "canceled":
                {
                    var (code, message) = ParseArmError(JsonSerializer.Serialize(body, JsonDefaults.Options));
                    throw new ArmException(500, code ?? status, message ?? "Azure 侧报告操作失败。");
                }
            }

            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds + 1, 10));
        }

        throw new ArmException(408, "operation_timeout", "等待 Azure 操作完成超时。");
    }

    // ---------------------------------------------------------------- 内部

    private Uri BuildUri(string pathOrUrl)
    {
        // 注意：.NET 在部分运行时上会把以 '/' 开头的相对路径（如 "/subscriptions?..."）
        // 当作 UriKind.Absolute 解析成功，但得到的其实是“没有 scheme 的相对 Uri”。
        // 这种 Uri 交给 SocketsHttpHandler 会被当成 file:// 本地路径而抛
        // “The 'file' scheme is not supported”。因此这里必须额外确认它是带 http/https
        // scheme 的真正绝对 URI，否则一律拼上 Endpoint 前缀。
        if (!string.IsNullOrEmpty(pathOrUrl)
            && Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absolute)
            && absolute.IsAbsoluteUri
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute;
        }

        return new Uri(Endpoint + (pathOrUrl.StartsWith('/') ? pathOrUrl : "/" + pathOrUrl));
    }

    private static List<JsonElement> ExtractItems(JsonElement? body)
    {
        var list = new List<JsonElement>();
        if (body is not { } root || root.ValueKind != JsonValueKind.Object)
        {
            return list;
        }

        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                list.Add(item.Clone());
            }
        }
        else if (!root.TryGetProperty("nextLink", out _))
        {
            // 单对象响应（例如 CostManagement query 的 properties）也当成一项返回。
            list.Add(root.Clone());
        }

        return list;
    }

    internal static (string? Code, string? Message) ParseArmError(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return (null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return (error.GetString(), null);
                }

                var code = error.TryGetProperty("code", out var c) ? c.GetString() : null;
                var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                return (code, message);
            }

            var topCode = root.TryGetProperty("code", out var tc) ? tc.GetString() : null;
            var topMessage = root.TryGetProperty("message", out var tm) ? tm.GetString() : null;
            return (topCode, topMessage);
        }
        catch (JsonException)
        {
            return (null, payload.Length > 400 ? payload[..400] : payload);
        }
    }

    public void Dispose() => _http.Dispose();
}

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
