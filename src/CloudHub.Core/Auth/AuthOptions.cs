namespace CloudHub.Core.Auth;

/// <summary>
/// 免注册即可用的公开客户端 ID。
/// Azure CLI 的 app registration 是 public client、已开启设备代码流、且对 ARM 有预授权，
/// 因此任何第三方工具都能直接借它做 device code 登录，无需自己去 Entra 里注册应用。
/// 用户也可以在「设置」里换成自己的 client id。
/// </summary>
public static class WellKnownClients
{
    public const string AzureCliClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";
    public const string AzurePowerShellClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
}

public sealed class AuthOptions
{
    /// <summary>签发机构主机，国内云（Azure China）可改为 https://login.chinacloudapi.cn 。</summary>
    public string AuthorityHost { get; set; } = "https://login.microsoftonline.com";

    /// <summary>租户 ID 或 common / organizations / consumers。</summary>
    public string TenantId { get; set; } = "common";

    public string ClientId { get; set; } = WellKnownClients.AzureCliClientId;

    /// <summary>目标资源（ARM）。Azure China 用 https://management.chinacloudapi.cn/ 。</summary>
    public string ArmResource { get; set; } = "https://management.azure.com/";

    public AuthOptions Clone() => new()
    {
        AuthorityHost = AuthorityHost,
        TenantId = TenantId,
        ClientId = ClientId,
        ArmResource = ArmResource,
    };

    /// <summary>设备代码流与刷新令牌共用的 scope 串。</summary>
    public string BuildScope() =>
        $"{ArmResource.TrimEnd('/')}/.default offline_access openid profile";

    public string DeviceCodeEndpoint => Endpoint("devicecode");

    public string TokenEndpoint => Endpoint("token");

    private string Endpoint(string leaf) =>
        $"{AuthorityHost.TrimEnd('/')}/{Uri.EscapeDataString(TenantId)}/oauth2/v2.0/{leaf}";

    public string Describe() =>
        $"tenant={TenantId} · client={ClientId} · arm={ArmResource}";
}
