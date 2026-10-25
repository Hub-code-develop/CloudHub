using CloudHub.Core.Arm;

namespace CloudHub.Desktop.Services;

/// <summary>一个云环境（公有云 / 中国云 / 美国政府云）的端点组合。</summary>
public sealed record CloudPreset(
    string Id,
    string DisplayName,
    string AuthorityHost,
    string ArmEndpoint,
    string ShortLabel)
{
    public string Description => $"{AuthorityHost} · {ArmEndpoint}";
}

public static class CloudPresets
{
    public static readonly CloudPreset AzurePublic = new(
        "public",
        "Azure 公有云",
        "https://login.microsoftonline.com",
        ArmClient.PublicArmEndpoint,
        "Public");

    public static readonly CloudPreset AzureChina = new(
        "china",
        "Azure 中国（由世纪互联运营）",
        "https://login.chinacloudapi.cn",
        ArmClient.ChinaArmEndpoint,
        "China");

    public static readonly CloudPreset AzureUsGovernment = new(
        "usgov",
        "Azure 美国政府",
        "https://login.microsoftonline.us",
        "https://management.usgovcloudapi.net",
        "USGov");

    public static readonly IReadOnlyList<CloudPreset> All = [AzurePublic, AzureChina, AzureUsGovernment];

    public static CloudPreset Resolve(string? id) =>
        All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? AzurePublic;

    /// <summary>根据端点反查云环境，用于把配置文件里手改过的端点映射回预设。</summary>
    public static CloudPreset Detect(string? authorityHost, string? armEndpoint)
    {
        foreach (var preset in All)
        {
            if (string.Equals(preset.ArmEndpoint, armEndpoint, StringComparison.OrdinalIgnoreCase)
                || string.Equals(preset.AuthorityHost, authorityHost, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        return AzurePublic;
    }
}

/// <summary>应用设置，落盘在 ~/.config/CloudHub/settings.json。</summary>
public sealed class AppSettings
{
    public string CloudId { get; set; } = CloudPresets.AzurePublic.Id;

    /// <summary>留空表示用预设里的地址；填了就覆盖（给自建 / 特殊环境用）。</summary>
    public string? CustomAuthorityHost { get; set; }

    public string? CustomArmEndpoint { get; set; }

    public string TenantId { get; set; } = "common";

    public string ClientId { get; set; } = CloudHub.Core.Auth.WellKnownClients.AzureCliClientId;

    /// <summary>Default / Light / Dark。</summary>
    public string Theme { get; set; } = "Default";

    public string? LastSubscriptionId { get; set; }

    public string? LastPageTag { get; set; }

    public bool IsPaneOpen { get; set; } = true;

    public AppSettings Clone() => new()
    {
        CloudId = CloudId,
        CustomAuthorityHost = CustomAuthorityHost,
        CustomArmEndpoint = CustomArmEndpoint,
        TenantId = TenantId,
        ClientId = ClientId,
        Theme = Theme,
        LastSubscriptionId = LastSubscriptionId,
        LastPageTag = LastPageTag,
        IsPaneOpen = IsPaneOpen,
    };

    public CloudPreset Cloud => CloudPresets.Resolve(CloudId);

    public string EffectiveAuthorityHost =>
        string.IsNullOrWhiteSpace(CustomAuthorityHost) ? Cloud.AuthorityHost : CustomAuthorityHost.Trim();

    public string EffectiveArmEndpoint =>
        string.IsNullOrWhiteSpace(CustomArmEndpoint) ? Cloud.ArmEndpoint : CustomArmEndpoint.Trim();

    public CloudHub.Core.Auth.AuthOptions ToAuthOptions() => new()
    {
        AuthorityHost = EffectiveAuthorityHost,
        TenantId = string.IsNullOrWhiteSpace(TenantId) ? "common" : TenantId.Trim(),
        ClientId = ClientId.Trim(),
        ArmResource = EffectiveArmEndpoint.TrimEnd('/') + "/",
    };
}
