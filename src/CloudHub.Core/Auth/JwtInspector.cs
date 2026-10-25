using System.Text.Json;

namespace CloudHub.Core.Auth;

/// <summary>
/// 只做一件事：把 JWT 的 payload 段解出来读 claim。
/// 完全不验签 —— 令牌是刚从 HTTPS 上的 token 端点拿回来的，这里只为了显示用户名。
/// </summary>
public static class JwtInspector
{
    public static JsonElement? TryReadPayload(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    public static UserProfile? DescribeUser(string? idToken)
    {
        var payload = TryReadPayload(idToken);
        if (payload is not { } p)
        {
            return null;
        }

        return new UserProfile
        {
            DisplayName = ReadString(p, "name"),
            UserPrincipalName = ReadString(p, "preferred_username") ?? ReadString(p, "upn") ?? ReadString(p, "unique_name") ?? ReadString(p, "email"),
            ObjectId = ReadString(p, "oid"),
            TenantId = ReadString(p, "tid"),
        };
    }

    /// <summary>从 access_token 里读 tid —— 多租户登录时真正的租户 ID 在这里。</summary>
    public static string? ReadTenantId(string? accessToken)
    {
        var payload = TryReadPayload(accessToken);
        return payload is { } p ? ReadString(p, "tid") : null;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s,
        };
        return Convert.FromBase64String(s);
    }
}
