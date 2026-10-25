using CloudHub.Core.Models;

namespace CloudHub.Core.Services;

public enum ResourceCategory
{
    Compute,
    Storage,
    Networking,
    Database,
    Web,
    Identity,
    Ai,
    Monitoring,
    Container,
    Integration,
    Other,
}

/// <summary>
/// 把 ARM 的资源类型映射成人话 + 分类。分类决定了 UI 里用哪个图标和配色，
/// 未收录的类型一律落到 Other，不做瞎猜。
/// </summary>
public static class ResourceTypeCatalog
{
    private static readonly Dictionary<string, (string Display, ResourceCategory Category)> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["microsoft.compute/virtualmachines"] = ("虚拟机", ResourceCategory.Compute),
            ["microsoft.compute/virtualmachinescalesets"] = ("虚拟机规模集", ResourceCategory.Compute),
            ["microsoft.compute/disks"] = ("托管磁盘", ResourceCategory.Compute),
            ["microsoft.compute/snapshots"] = ("磁盘快照", ResourceCategory.Compute),
            ["microsoft.compute/images"] = ("托管映像", ResourceCategory.Compute),
            ["microsoft.compute/availabilitysets"] = ("可用性集", ResourceCategory.Compute),

            ["microsoft.storage/storageaccounts"] = ("存储账户", ResourceCategory.Storage),
            ["microsoft.storage/storageaccounts/blobservices"] = ("Blob 服务", ResourceCategory.Storage),
            ["microsoft.storage/storageaccounts/fileservices"] = ("文件服务", ResourceCategory.Storage),
            ["microsoft.datalakestore/accounts"] = ("Data Lake", ResourceCategory.Storage),
            ["microsoft.recoveryservices/vaults"] = ("恢复服务保管库", ResourceCategory.Storage),
            ["microsoft.backupprotectioncontainers"] = ("备份容器", ResourceCategory.Storage),

            ["microsoft.network/virtualnetworks"] = ("虚拟网络", ResourceCategory.Networking),
            ["microsoft.network/publicipaddresses"] = ("公共 IP 地址", ResourceCategory.Networking),
            ["microsoft.network/networksecuritygroups"] = ("网络安全组", ResourceCategory.Networking),
            ["microsoft.network/networkinterfaces"] = ("网络接口", ResourceCategory.Networking),
            ["microsoft.network/loadbalancers"] = ("负载均衡器", ResourceCategory.Networking),
            ["microsoft.network/applicationgateways"] = ("应用程序网关", ResourceCategory.Networking),
            ["microsoft.network/azurefirewalls"] = ("Azure 防火墙", ResourceCategory.Networking),
            ["microsoft.network/routetables"] = ("路由表", ResourceCategory.Networking),
            ["microsoft.network/privateendpoints"] = ("专用终结点", ResourceCategory.Networking),
            ["microsoft.network/privatednszones"] = ("专用 DNS 区域", ResourceCategory.Networking),
            ["microsoft.network/dnszones"] = ("DNS 区域", ResourceCategory.Networking),
            ["microsoft.cdn/profiles"] = ("CDN 配置文件", ResourceCategory.Networking),
            ["microsoft.network/frontdoors"] = ("Front Door", ResourceCategory.Networking),
            ["microsoft.network/bastionhosts"] = ("Bastion", ResourceCategory.Networking),

            ["microsoft.sql/servers"] = ("SQL 服务器", ResourceCategory.Database),
            ["microsoft.sql/servers/databases"] = ("SQL 数据库", ResourceCategory.Database),
            ["microsoft.dbforpostgresql/flexibleservers"] = ("PostgreSQL 灵活服务器", ResourceCategory.Database),
            ["microsoft.dbformysql/flexibleservers"] = ("MySQL 灵活服务器", ResourceCategory.Database),
            ["microsoft.documentdb/databaseaccounts"] = ("Cosmos DB", ResourceCategory.Database),
            ["microsoft.cache/redis"] = ("Redis 缓存", ResourceCategory.Database),
            ["microsoft.dbformariadb/servers"] = ("MariaDB 服务器", ResourceCategory.Database),

            ["microsoft.web/sites"] = ("应用服务 / 函数应用", ResourceCategory.Web),
            ["microsoft.web/serverfarms"] = ("应用服务计划", ResourceCategory.Web),
            ["microsoft.web/staticsites"] = ("静态 Web 应用", ResourceCategory.Web),

            ["microsoft.containerservice/managedclusters"] = ("Kubernetes 服务", ResourceCategory.Container),
            ["microsoft.containerregistry/registries"] = ("容器注册表", ResourceCategory.Container),
            ["microsoft.containerinstance/containergroups"] = ("容器实例", ResourceCategory.Container),
            ["microsoft.app/managedenvironments"] = ("容器应用环境", ResourceCategory.Container),
            ["microsoft.app/containerapps"] = ("容器应用", ResourceCategory.Container),

            ["microsoft.cognitiveservices/accounts"] = ("认知服务", ResourceCategory.Ai),
            ["microsoft.machinelearningservices/workspaces"] = ("机器学习工作区", ResourceCategory.Ai),
            ["microsoft.search/searchservices"] = ("认知搜索", ResourceCategory.Ai),
            ["microsoft.botservice/botservices"] = ("机器人服务", ResourceCategory.Ai),

            ["microsoft.keyvault/vaults"] = ("密钥保管库", ResourceCategory.Identity),
            ["microsoft.managedidentity/userassignedidentities"] = ("用户分配的托管标识", ResourceCategory.Identity),

            ["microsoft.insights/components"] = ("Application Insights", ResourceCategory.Monitoring),
            ["microsoft.insights/actiongroups"] = ("操作组", ResourceCategory.Monitoring),
            ["microsoft.insights/autoscalesettings"] = ("自动缩放设置", ResourceCategory.Monitoring),
            ["microsoft.operationalinsights/workspaces"] = ("Log Analytics 工作区", ResourceCategory.Monitoring),
            ["microsoft.insights/alertrules"] = ("警报规则", ResourceCategory.Monitoring),
            ["microsoft.insights/metricalerts"] = ("指标警报", ResourceCategory.Monitoring),

            ["microsoft.logic/workflows"] = ("逻辑应用", ResourceCategory.Integration),
            ["microsoft.servicebus/namespaces"] = ("服务总线", ResourceCategory.Integration),
            ["microsoft.eventhub/namespaces"] = ("事件中心", ResourceCategory.Integration),
            ["microsoft.eventgrid/topics"] = ("事件网格主题", ResourceCategory.Integration),
            ["microsoft.apimanagement/service"] = ("API 管理", ResourceCategory.Integration),
            ["microsoft.automation/automationaccounts"] = ("自动化账户", ResourceCategory.Integration),
        };

    public static (string Display, ResourceCategory Category) Describe(string resourceType)
    {
        if (Map.TryGetValue(resourceType, out var entry))
        {
            return entry;
        }

        // 未收录：退化成类型的最后一段，比原样显示 Microsoft.Xxx/yyy 好看，也不算编造。
        var shortType = resourceType.LastIndexOf('/') is var i && i >= 0 ? resourceType[(i + 1)..] : resourceType;
        return (Humanize(shortType), ResourceCategory.Other);
    }

    public static string DisplayName(ArmResource resource)
    {
        var (display, _) = Describe(resource.Type);
        return display;
    }

    public static ResourceCategory CategoryOf(ArmResource resource) => Describe(resource.Type).Category;

    /// <summary>分类 → 中文标签（用于 UI 分类筛选与导航）。</summary>
    public static string CategoryDisplayName(ResourceCategory category) => category switch
    {
        ResourceCategory.Compute => "计算",
        ResourceCategory.Storage => "存储",
        ResourceCategory.Networking => "网络",
        ResourceCategory.Database => "数据库",
        ResourceCategory.Web => "Web",
        ResourceCategory.Container => "容器",
        ResourceCategory.Identity => "身份与安全",
        ResourceCategory.Ai => "人工智能",
        ResourceCategory.Monitoring => "监控",
        ResourceCategory.Integration => "集成",
        _ => "其他",
    };

    /// <summary>全部分类（固定展示顺序），UI 分类筛选与导航按此枚举。</summary>
    public static IReadOnlyList<ResourceCategory> Categories { get; } =
    [
        ResourceCategory.Compute,
        ResourceCategory.Storage,
        ResourceCategory.Networking,
        ResourceCategory.Database,
        ResourceCategory.Web,
        ResourceCategory.Container,
        ResourceCategory.Identity,
        ResourceCategory.Ai,
        ResourceCategory.Monitoring,
        ResourceCategory.Integration,
        ResourceCategory.Other,
    ];

    /// <summary>把 PascalCase / camelCase 类型名拆成可读的分词。</summary>
    private static string Humanize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "未知类型";
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }

        return builder.ToString();
    }
}
