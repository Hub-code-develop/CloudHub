using System.Collections.Generic;

namespace CloudHub.Core.Services;

/// <summary>
/// Azure 门户「所有服务」的完整服务目录（按官方分类录入）。
/// 用于 CloudHub 的「所有服务」导航页：列出 Azure 提供的每一项服务，点击可在 Azure 门户打开对应页。
/// 数据来源：Azure 门户「所有服务」分类（2026-10 快照），已跳过纯标签行（独立的「预览」「合作伙伴」）。
/// </summary>
public enum AzureServiceCategoryKey
{
    AiMl,
    DevOps,
    ManagementGovernance,
    WebMobile,
    Analytics,
    Storage,
    Security,
    Container,
    Database,
    Identity,
    HybridMulticloud,
    Compute,
    General,
    Iot,
    Monitor,
    Network,
    Migration,
    Integration,
}

/// <summary>一个 Azure 服务分类（含中文显示名与服务计数）。</summary>
public sealed record AzureServiceCategory(AzureServiceCategoryKey Key, string DisplayName, int Count);

/// <summary>单个 Azure 服务条目。</summary>
public sealed record AzureServiceItem(string Name, AzureServiceCategoryKey Category, string? BrowseType = null)
{
    /// <summary>
    /// 点击后在浏览器打开的 Azure 门户地址。能映射到 ARM 资源类型的用 #browse 直达；
    /// 否则回退到门户首页（用户可自行搜索）。
    /// </summary>
    public string PortalUrl => BrowseType is { } t
        ? $"https://portal.azure.com/#browse/{t}"
        : "https://portal.azure.com/#home";
}

/// <summary>静态服务目录：分类 + 全量服务项。</summary>
public static class AzureServiceCatalog
{
    public static IReadOnlyList<AzureServiceCategory> Categories { get; } =
    [
        new(AzureServiceCategoryKey.AiMl, "AI + 机器学习", 20),
        new(AzureServiceCategoryKey.DevOps, "DevOps", 22),
        new(AzureServiceCategoryKey.ManagementGovernance, "管理与治理", 33),
        new(AzureServiceCategoryKey.WebMobile, "Web 与移动", 22),
        new(AzureServiceCategoryKey.Analytics, "分析", 19),
        new(AzureServiceCategoryKey.Storage, "存储", 17),
        new(AzureServiceCategoryKey.Security, "安全性", 20),
        new(AzureServiceCategoryKey.Container, "容器", 11),
        new(AzureServiceCategoryKey.Database, "数据库", 16),
        new(AzureServiceCategoryKey.Identity, "标识", 18),
        new(AzureServiceCategoryKey.HybridMulticloud, "混合 + 多云", 19),
        new(AzureServiceCategoryKey.Compute, "计算", 32),
        new(AzureServiceCategoryKey.General, "常规", 10),
        new(AzureServiceCategoryKey.Iot, "物联网", 19),
        new(AzureServiceCategoryKey.Monitor, "监视器", 25),
        new(AzureServiceCategoryKey.Network, "网络", 27),
        new(AzureServiceCategoryKey.Migration, "迁移", 5),
        new(AzureServiceCategoryKey.Integration, "集成", 15),
    ];

    public static IReadOnlyList<AzureServiceItem> AllServices { get; } = Build();

    private static IReadOnlyList<AzureServiceItem> Build()
    {
        var list = new List<AzureServiceItem>();
        void Add(string name, AzureServiceCategoryKey cat, string? browse = null)
            => list.Add(new AzureServiceItem(name, cat, browse));

        // ----------------------------------------------------- AI + 机器学习 (20)
        Add("Azure Machine Learning", AzureServiceCategoryKey.AiMl, "Microsoft.MachineLearningServices/workspaces");
        Add("沉浸式阅读器", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("翻译工具", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("个性化体验创建服务", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("计算机视觉", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("内容审查器", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("人脸 API", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("文档智能", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("异常检测器", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("语言", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("语音服务", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("指标顾问", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("自定义视觉", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("Azure AI 视频索引器", AzureServiceCategoryKey.AiMl);
        Add("AI 搜索(Foundry IQ)", AzureServiceCategoryKey.AiMl, "Microsoft.Search/searchServices");
        Add("Bot Services", AzureServiceCategoryKey.AiMl, "Microsoft.BotService/botServices");
        Add("Microsoft Foundry", AzureServiceCategoryKey.AiMl);
        Add("Azure OpenAI", AzureServiceCategoryKey.AiMl, "Microsoft.CognitiveServices/accounts");
        Add("Anyscale 云", AzureServiceCategoryKey.AiMl);
        Add("Azure Synapse Analytics", AzureServiceCategoryKey.AiMl, "Microsoft.Synapse/workspaces");

        // ----------------------------------------------------- DevOps (22)
        Add("Azure 部署环境", AzureServiceCategoryKey.DevOps, "Microsoft.DevCenter/devcenters");
        Add("Azure 负载测试", AzureServiceCategoryKey.DevOps, "Microsoft.LoadTestService/loadTests");
        Add("开发人员中心", AzureServiceCategoryKey.DevOps, "Microsoft.DevCenter/devcenters");
        Add("Azure 实验室服务", AzureServiceCategoryKey.DevOps, "Microsoft.LabServices/labPlans");
        Add("网络连接", AzureServiceCategoryKey.DevOps);
        Add("项目", AzureServiceCategoryKey.DevOps);
        Add("Chaos Studio", AzureServiceCategoryKey.DevOps, "Microsoft.Chaos/chaosExperiments");
        Add("Azure DevOps 组织", AzureServiceCategoryKey.DevOps);
        Add("DevTest Labs", AzureServiceCategoryKey.DevOps, "Microsoft.DevTestLab/labs");
        Add("GitHub", AzureServiceCategoryKey.DevOps);
        Add("Managed DevOps Pools", AzureServiceCategoryKey.DevOps);
        Add("Microsoft Dev Box", AzureServiceCategoryKey.DevOps, "Microsoft.DevBox/projects");
        Add("Playwright Testing (Classic)", AzureServiceCategoryKey.DevOps);
        Add("应用程序配置", AzureServiceCategoryKey.DevOps, "Microsoft.AppConfiguration/configurationStores");
        Add("API 管理服务", AzureServiceCategoryKey.DevOps, "Microsoft.ApiManagement/service");
        Add("API connections", AzureServiceCategoryKey.DevOps);
        Add("弹性云(Elasticsearch)", AzureServiceCategoryKey.DevOps);
        Add("监视", AzureServiceCategoryKey.DevOps);
        Add("Application Insights", AzureServiceCategoryKey.DevOps, "Microsoft.Insights/components");
        Add("Datadog - Azure Native ISV 服务", AzureServiceCategoryKey.DevOps);
        Add("Azure Native Dynatrace Service", AzureServiceCategoryKey.DevOps);
        Add("Azure Native New Relic Service", AzureServiceCategoryKey.DevOps);

        // ----------------------------------------------------- 管理与治理 (33)
        Add("订阅", AzureServiceCategoryKey.ManagementGovernance);
        Add("更改分析", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.ChangeAnalysis/instances");
        Add("推出", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Resources/deployments");
        Add("诊断设置", AzureServiceCategoryKey.ManagementGovernance);
        Add("Azure 资源转移器", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Migrate/moveCollections");
        Add("Azure Center for SAP 解决方案", AzureServiceCategoryKey.ManagementGovernance);
        Add("Deployment Scripts", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Resources/deploymentScripts");
        Add("Azure Monitors for SAP Solutions", AzureServiceCategoryKey.ManagementGovernance);
        Add("SAP 解决方案虚拟实例", AzureServiceCategoryKey.ManagementGovernance);
        Add("Template specs", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Resources/templateSpecs");
        Add("Azure Copilot 管理中心", AzureServiceCategoryKey.ManagementGovernance);
        Add("策略", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Authorization/policyAssignments");
        Add("产能预留组", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Compute/capacityReservationGroups");
        Add("成本管理", AzureServiceCategoryKey.ManagementGovernance);
        Add("Azure 更新管理器", AzureServiceCategoryKey.ManagementGovernance);
        Add("顾问", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Advisor/configurations");
        Add("来宾分配", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.GuestConfiguration/guestConfigurationAssignments");
        Add("蓝图", AzureServiceCategoryKey.ManagementGovernance);
        Add("碳优化", AzureServiceCategoryKey.ManagementGovernance);
        Add("自动化帐户", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Automation/automationAccounts");
        Add("Automanage", AzureServiceCategoryKey.ManagementGovernance);
        Add("Microsoft Purview 帐户", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.Purview/accounts");
        Add("托管应用程序中心", AzureServiceCategoryKey.ManagementGovernance);
        Add("Customer Lockbox for Microsoft Azure", AzureServiceCategoryKey.ManagementGovernance);
        Add("Azure Lighthouse", AzureServiceCategoryKey.ManagementGovernance);
        Add("通用打印", AzureServiceCategoryKey.ManagementGovernance);
        Add("托管桌面", AzureServiceCategoryKey.ManagementGovernance);
        Add("Intune", AzureServiceCategoryKey.ManagementGovernance);
        Add("Intune 教育版", AzureServiceCategoryKey.ManagementGovernance);
        Add("备份保管库", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.DataProtection/backupVaults");
        Add("复原能力", AzureServiceCategoryKey.ManagementGovernance);
        Add("Azure Native Qumulo Scalable File Service", AzureServiceCategoryKey.ManagementGovernance);
        Add("Recovery Services 保管库", AzureServiceCategoryKey.ManagementGovernance, "Microsoft.RecoveryServices/vaults");

        // ----------------------------------------------------- Web 与移动 (22)
        Add("API 管理服务", AzureServiceCategoryKey.WebMobile, "Microsoft.ApiManagement/service");
        Add("API connections", AzureServiceCategoryKey.WebMobile);
        Add("应用程序配置", AzureServiceCategoryKey.WebMobile, "Microsoft.AppConfiguration/configurationStores");
        Add("应用服务环境", AzureServiceCategoryKey.WebMobile, "Microsoft.Web/hostingEnvironments");
        Add("应用服务计划", AzureServiceCategoryKey.WebMobile, "Microsoft.Web/serverFarms");
        Add("应用服务域", AzureServiceCategoryKey.WebMobile, "Microsoft.DomainRegistration/domains");
        Add("应用服务证书", AzureServiceCategoryKey.WebMobile, "Microsoft.DomainRegistration/certificates");
        Add("Application Insights", AzureServiceCategoryKey.WebMobile, "Microsoft.Insights/components");
        Add("函数应用", AzureServiceCategoryKey.WebMobile, "Microsoft.Web/sites");
        Add("容器应用", AzureServiceCategoryKey.WebMobile, "Microsoft.App/containerApps");
        Add("应用服务", AzureServiceCategoryKey.WebMobile, "Microsoft.Web/sites");
        Add("Logic apps", AzureServiceCategoryKey.WebMobile, "Microsoft.Logic/workflows");
        Add("Azure Spring Apps", AzureServiceCategoryKey.WebMobile, "Microsoft.AppPlatform/spring");
        Add("Static Web Apps", AzureServiceCategoryKey.WebMobile, "Microsoft.Web/staticSites");
        Add("AI 搜索(Foundry IQ)", AzureServiceCategoryKey.WebMobile, "Microsoft.Search/searchServices");
        Add("Communication Services", AzureServiceCategoryKey.WebMobile, "Microsoft.Communication/communicationServices");
        Add("Email Communication Services", AzureServiceCategoryKey.WebMobile, "Microsoft.Communication/emailServices");
        Add("Fluid Relay", AzureServiceCategoryKey.WebMobile, "Microsoft.FluidRelay/fluidRelayServers");
        Add("Notification Hubs", AzureServiceCategoryKey.WebMobile, "Microsoft.NotificationHubs/namespaces");
        Add("SignalR", AzureServiceCategoryKey.WebMobile, "Microsoft.SignalRService/signalR");
        Add("Web PubSub 服务", AzureServiceCategoryKey.WebMobile, "Microsoft.SignalRService/webPubSub");
        Add("Web PubSub for Socket.IO", AzureServiceCategoryKey.WebMobile, "Microsoft.SignalRService/webPubSub");

        // ----------------------------------------------------- 分析 (19)
        Add("Analysis Services", AzureServiceCategoryKey.Analytics, "Microsoft.AnalysisServices/servers");
        Add("Data Lake Analytics", AzureServiceCategoryKey.Analytics);
        Add("Data Lake Storage Gen1", AzureServiceCategoryKey.Analytics);
        Add("Azure Databricks", AzureServiceCategoryKey.Analytics, "Microsoft.Databricks/workspaces");
        Add("HDInsight 群集", AzureServiceCategoryKey.Analytics, "Microsoft.HDInsight/clusters");
        Add("Informatica Intelligent Data Management Cloud", AzureServiceCategoryKey.Analytics);
        Add("Microsoft Graph Data Connect", AzureServiceCategoryKey.Analytics);
        Add("Azure 数据资源管理器群集", AzureServiceCategoryKey.Analytics, "Microsoft.Kusto/clusters");
        Add("Data Share Invitations", AzureServiceCategoryKey.Analytics);
        Add("Data Shares", AzureServiceCategoryKey.Analytics, "Microsoft.DataShare/accounts");
        Add("Power BI Embedded", AzureServiceCategoryKey.Analytics, "Microsoft.PowerBIEmbedded/workspaces");
        Add("流分析群集", AzureServiceCategoryKey.Analytics, "Microsoft.StreamAnalytics/clusters");
        Add("流分析作业", AzureServiceCategoryKey.Analytics, "Microsoft.StreamAnalytics/streamingJobs");
        Add("事件中心", AzureServiceCategoryKey.Analytics, "Microsoft.EventHub/namespaces");
        Add("托管的 Prometheus", AzureServiceCategoryKey.Analytics, "Microsoft.Monitor/accounts");
        Add("Apache Kafka & Apache Flink on Confluent Cloud", AzureServiceCategoryKey.Analytics);
        Add("Log Analytics 工作区", AzureServiceCategoryKey.Analytics, "Microsoft.OperationalInsights/workspaces");
        Add("Azure Synapse Analytics", AzureServiceCategoryKey.Analytics, "Microsoft.Synapse/workspaces");
        Add("Azure Synapse Analytics (private link hubs)", AzureServiceCategoryKey.Analytics);

        // ----------------------------------------------------- 存储 (17)
        Add("Azure Edge Hardware Center", AzureServiceCategoryKey.Storage);
        Add("Azure Stack Edge / Data Box Gateway", AzureServiceCategoryKey.Storage);
        Add("磁盘", AzureServiceCategoryKey.Storage, "Microsoft.Compute/disks");
        Add("磁盘访问权限", AzureServiceCategoryKey.Storage, "Microsoft.Compute/diskAccesses");
        Add("磁盘加密集", AzureServiceCategoryKey.Storage, "Microsoft.Compute/diskEncryptionSets");
        Add("存储浏览器", AzureServiceCategoryKey.Storage);
        Add("存储帐户", AzureServiceCategoryKey.Storage, "Microsoft.Storage/storageAccounts");
        Add("弹性 SAN", AzureServiceCategoryKey.Storage, "Microsoft.ElasticSan/elasticSans");
        Add("快照", AzureServiceCategoryKey.Storage, "Microsoft.Compute/snapshots");
        Add("Azure Native Pure Storage Cloud Service", AzureServiceCategoryKey.Storage);
        Add("Azure Native Qumulo Scalable File Service", AzureServiceCategoryKey.Storage);
        Add("Azure NetApp 文件", AzureServiceCategoryKey.Storage, "Microsoft.NetApp/netAppAccounts");
        Add("存储移动程序", AzureServiceCategoryKey.Storage);
        Add("Azure Data Box", AzureServiceCategoryKey.Storage);
        Add("存储同步服务", AzureServiceCategoryKey.Storage, "Microsoft.StorageSync/storageSyncServices");
        Add("Data Lake Storage Gen1", AzureServiceCategoryKey.Storage);
        Add("Azure Managed Lustre", AzureServiceCategoryKey.Storage);

        // ----------------------------------------------------- 安全性 (20)
        Add("App Compliance Automation Tool for Microsoft 365", AzureServiceCategoryKey.Security);
        Add("Confidential Ledgers", AzureServiceCategoryKey.Security, "Microsoft.ConfidentialLedger/ledgers");
        Add("Log Analytics 工作区", AzureServiceCategoryKey.Security, "Microsoft.OperationalInsights/workspaces");
        Add("Web 应用程序防火墙策略(WAF)", AzureServiceCategoryKey.Security, "Microsoft.Network/applicationGatewayWebApplicationFirewallPolicies");
        Add("多重身份验证", AzureServiceCategoryKey.Security);
        Add("工件签名帐户", AzureServiceCategoryKey.Security);
        Add("Microsoft Entra 域服务", AzureServiceCategoryKey.Security, "Microsoft.AAD/DomainServices");
        Add("Microsoft Entra ID", AzureServiceCategoryKey.Security);
        Add("Microsoft Entra ID 安全性", AzureServiceCategoryKey.Security);
        Add("Microsoft Entra Privileged Identity Management", AzureServiceCategoryKey.Security);
        Add("密钥保管库", AzureServiceCategoryKey.Security, "Microsoft.KeyVault/vaults");
        Add("Azure 云 HSM", AzureServiceCategoryKey.Security);
        Add("DDoS 防护计划", AzureServiceCategoryKey.Security, "Microsoft.Network/ddosProtectionPlans");
        Add("Firewalls", AzureServiceCategoryKey.Security, "Microsoft.Network/azureFirewalls");
        Add("Azure Key Vault 托管 HSM", AzureServiceCategoryKey.Security, "Microsoft.KeyVault/managedHSMs");
        Add("Virtual network gateways", AzureServiceCategoryKey.Security, "Microsoft.Network/virtualNetworkGateways");
        Add("Microsoft Defender EASM", AzureServiceCategoryKey.Security);
        Add("Microsoft Defender for Cloud", AzureServiceCategoryKey.Security);
        Add("Microsoft Defender for IoT", AzureServiceCategoryKey.Security);
        Add("Microsoft Sentinel", AzureServiceCategoryKey.Security);

        // ----------------------------------------------------- 容器 (11)
        Add("容器实例", AzureServiceCategoryKey.Container, "Microsoft.ContainerInstance/containerGroups");
        Add("Container registries", AzureServiceCategoryKey.Container, "Microsoft.ContainerRegistry/registries");
        Add("Kubernetes 服务", AzureServiceCategoryKey.Container, "Microsoft.ContainerService/managedClusters");
        Add("Kubernetes 服务 - 自动", AzureServiceCategoryKey.Container, "Microsoft.ContainerService/managedClusters");
        Add("Kubernetes 舰队管理器", AzureServiceCategoryKey.Container, "Microsoft.ContainerService/fleets");
        Add("Kubernetes 中心", AzureServiceCategoryKey.Container);
        Add("Service Fabric 群集", AzureServiceCategoryKey.Container, "Microsoft.ServiceFabric/clusters");
        Add("Service Fabric 托管群集", AzureServiceCategoryKey.Container, "Microsoft.ServiceFabricManagedClusters/managedClusters");
        Add("容器应用", AzureServiceCategoryKey.Container, "Microsoft.App/containerApps");
        Add("容器应用作业", AzureServiceCategoryKey.Container, "Microsoft.App/jobs");
        Add("应用程序配置", AzureServiceCategoryKey.Container, "Microsoft.AppConfiguration/configurationStores");

        // ----------------------------------------------------- 数据库 (16)
        Add("Azure Cosmos DB", AzureServiceCategoryKey.Database, "Microsoft.DocumentDB/databaseAccounts");
        Add("Azure Database for PostgreSQL 灵活服务器", AzureServiceCategoryKey.Database, "Microsoft.DBforPostgreSQL/flexibleServers");
        Add("Azure HorizonDB (预览版)", AzureServiceCategoryKey.Database);
        Add("Azure Database for MySQL 灵活服务器", AzureServiceCategoryKey.Database, "Microsoft.DBforMySQL/flexibleServers");
        Add("Azure DocumentDB (具有 MongoDB 兼容性)", AzureServiceCategoryKey.Database, "Microsoft.DocumentDB/databaseAccounts");
        Add("Azure Managed Instance for Apache Cassandra", AzureServiceCategoryKey.Database);
        Add("MongoDB Atlas", AzureServiceCategoryKey.Database);
        Add("Oracle Database@Azure", AzureServiceCategoryKey.Database);
        Add("Azure SQL", AzureServiceCategoryKey.Database, "Microsoft.Sql/servers");
        Add("Azure Arc 数据控制器", AzureServiceCategoryKey.Database, "Microsoft.AzureArcData/dataControllers");
        Add("SQL 托管实例 - Azure Arc", AzureServiceCategoryKey.Database, "Microsoft.AzureArcData/sqlManagedInstances");
        Add("SQL Server 实例", AzureServiceCategoryKey.Database, "Microsoft.SqlVirtualMachine/SqlVirtualMachines");
        Add("代管数据库", AzureServiceCategoryKey.Database);
        Add("Azure Cache for Redis", AzureServiceCategoryKey.Database, "Microsoft.Cache/redis");
        Add("Azure Database Migration Services", AzureServiceCategoryKey.Database, "Microsoft.DataMigration/services");
        Add("Azure Managed Redis", AzureServiceCategoryKey.Database, "Microsoft.Cache/redisEnterprise");

        // ----------------------------------------------------- 标识 (18)
        Add("Microsoft Entra Connect Health", AzureServiceCategoryKey.Identity);
        Add("企业应用程序", AzureServiceCategoryKey.Identity);
        Add("外部标识", AzureServiceCategoryKey.Identity);
        Add("Azure AD B2C", AzureServiceCategoryKey.Identity);
        Add("B2C 租户", AzureServiceCategoryKey.Identity);
        Add("来宾使用情况", AzureServiceCategoryKey.Identity);
        Add("托管标识", AzureServiceCategoryKey.Identity, "Microsoft.ManagedIdentity/userAssignedIdentities");
        Add("外部配置租户", AzureServiceCategoryKey.Identity);
        Add("应用注册", AzureServiceCategoryKey.Identity);
        Add("Agent IDs", AzureServiceCategoryKey.Identity);
        Add("Azure Databricks 的访问连接器", AzureServiceCategoryKey.Identity);
        Add("Microsoft Entra 域服务", AzureServiceCategoryKey.Identity, "Microsoft.AAD/DomainServices");
        Add("Microsoft Entra ID", AzureServiceCategoryKey.Identity);
        Add("Microsoft Entra Privileged Identity Management", AzureServiceCategoryKey.Identity);
        Add("验证 ID", AzureServiceCategoryKey.Identity);
        Add("Identity Governance", AzureServiceCategoryKey.Identity);
        Add("Microsoft Entra ID 安全性", AzureServiceCategoryKey.Identity);
        Add("Microsoft Entra ID 保护", AzureServiceCategoryKey.Identity);

        // ----------------------------------------------------- 混合 + 多云 (19)
        Add("来宾分配", AzureServiceCategoryKey.HybridMulticloud, "Microsoft.GuestConfiguration/guestConfigurationAssignments");
        Add("Azure 网络功能管理器 - 设备", AzureServiceCategoryKey.HybridMulticloud);
        Add("站点管理器 - Azure Arc", AzureServiceCategoryKey.HybridMulticloud);
        Add("Azure Arc 数据控制器", AzureServiceCategoryKey.HybridMulticloud, "Microsoft.AzureArcData/dataControllers");
        Add("Microsoft Entra ID", AzureServiceCategoryKey.HybridMulticloud);
        Add("Azure VMware 解决方案", AzureServiceCategoryKey.HybridMulticloud, "Microsoft.AVS/privateClouds");
        Add("Azure Arc", AzureServiceCategoryKey.HybridMulticloud, "Microsoft.HybridCompute/machines");
        Add("运营商关系", AzureServiceCategoryKey.HybridMulticloud);
        Add("Azure Edge Hardware Center", AzureServiceCategoryKey.HybridMulticloud);
        Add("ExpressRoute circuits", AzureServiceCategoryKey.HybridMulticloud, "Microsoft.Network/expressRouteCircuits");
        Add("Azure Local", AzureServiceCategoryKey.HybridMulticloud);
        Add("Azure Multicloud Interconnects", AzureServiceCategoryKey.HybridMulticloud);
        Add("SQL Server 实例", AzureServiceCategoryKey.HybridMulticloud, "Microsoft.SqlVirtualMachine/SqlVirtualMachines");
        Add("Virtual WANs", AzureServiceCategoryKey.HybridMulticloud, "Microsoft.Network/virtualWans");
        Add("SQL 托管实例 - Azure Arc", AzureServiceCategoryKey.HybridMulticloud, "Microsoft.AzureArcData/sqlManagedInstances");
        Add("SQL Server 数据库", AzureServiceCategoryKey.HybridMulticloud);
        Add("Microsoft Entra Connect Health", AzureServiceCategoryKey.HybridMulticloud);
        Add("Microsoft Defender for Cloud", AzureServiceCategoryKey.HybridMulticloud);
        Add("Microsoft Sentinel", AzureServiceCategoryKey.HybridMulticloud);

        // ----------------------------------------------------- 计算 (32)
        Add("还原点集合", AzureServiceCategoryKey.Compute, "Microsoft.Compute/restorePointCollections");
        Add("计算基础结构", AzureServiceCategoryKey.Compute);
        Add("计算舰队", AzureServiceCategoryKey.Compute);
        Add("Azure 计算库", AzureServiceCategoryKey.Compute, "Microsoft.Compute/galleries");
        Add("可用性集", AzureServiceCategoryKey.Compute, "Microsoft.Compute/availabilitySets");
        Add("邻近放置组", AzureServiceCategoryKey.Compute, "Microsoft.Compute/proximityPlacementGroups");
        Add("社区映像", AzureServiceCategoryKey.Compute);
        Add("实验室帐户", AzureServiceCategoryKey.Compute);
        Add("虚拟机", AzureServiceCategoryKey.Compute, "Microsoft.Compute/virtualMachines");
        Add("虚拟机规模集", AzureServiceCategoryKey.Compute, "Microsoft.Compute/virtualMachineScaleSets");
        Add("Azure 虚拟桌面", AzureServiceCategoryKey.Compute, "Microsoft.DesktopVirtualization/hostPools");
        Add("映像", AzureServiceCategoryKey.Compute, "Microsoft.Compute/images");
        Add("映像模板", AzureServiceCategoryKey.Compute);
        Add("主机组", AzureServiceCategoryKey.Compute, "Microsoft.Compute/hostGroups");
        Add("SSH 密钥", AzureServiceCategoryKey.Compute, "Microsoft.Compute/sshPublicKeys");
        Add("VM 应用程序版本", AzureServiceCategoryKey.Compute);
        Add("VM 应用程序定义", AzureServiceCategoryKey.Compute);
        Add("VM 映像版本", AzureServiceCategoryKey.Compute);
        Add("VM 映像定义", AzureServiceCategoryKey.Compute);
        Add("应用服务", AzureServiceCategoryKey.Compute, "Microsoft.Web/sites");
        Add("云服务(外延支持)", AzureServiceCategoryKey.Compute, "Microsoft.Compute/cloudServices");
        Add("SAP 解决方案虚拟实例", AzureServiceCategoryKey.Compute);
        Add("Azure Spring Apps", AzureServiceCategoryKey.Compute, "Microsoft.AppPlatform/spring");
        Add("函数应用", AzureServiceCategoryKey.Compute, "Microsoft.Web/sites");
        Add("容器应用", AzureServiceCategoryKey.Compute, "Microsoft.App/containerApps");
        Add("容器应用环境", AzureServiceCategoryKey.Compute, "Microsoft.App/managedEnvironments");
        Add("Kubernetes 服务", AzureServiceCategoryKey.Compute, "Microsoft.ContainerService/managedClusters");
        Add("Kubernetes 服务 - 自动", AzureServiceCategoryKey.Compute, "Microsoft.ContainerService/managedClusters");
        Add("批处理帐户", AzureServiceCategoryKey.Compute, "Microsoft.Batch/batchAccounts");
        Add("BareMetal Instances", AzureServiceCategoryKey.Compute);
        Add("Quantum Workspaces", AzureServiceCategoryKey.Compute);
        Add("SAP HANA on Azure", AzureServiceCategoryKey.Compute);

        // ----------------------------------------------------- 常规 (10)
        Add("成本管理 + 计费", AzureServiceCategoryKey.General);
        Add("免费服务", AzureServiceCategoryKey.General);
        Add("配额", AzureServiceCategoryKey.General);
        Add("预留", AzureServiceCategoryKey.General);
        Add("资源管理器", AzureServiceCategoryKey.General);
        Add("快速入门中心", AzureServiceCategoryKey.General);
        Add("预览功能", AzureServiceCategoryKey.General);
        Add("Education", AzureServiceCategoryKey.General);
        Add("帮助 + 支持", AzureServiceCategoryKey.General);
        Add("服务运行状况", AzureServiceCategoryKey.General);

        // ----------------------------------------------------- 物联网 (19)
        Add("IoT 中心", AzureServiceCategoryKey.Iot, "Microsoft.Devices/IotHubs");
        Add("IoT Central Applications", AzureServiceCategoryKey.Iot, "Microsoft.IoTCentral/iotApps");
        Add("适用于 IoT 中心的设备更新", AzureServiceCategoryKey.Iot);
        Add("Azure IoT 中心设备预配服务", AzureServiceCategoryKey.Iot, "Microsoft.Devices/provisioningServices");
        Add("Microsoft Defender for IoT", AzureServiceCategoryKey.Iot);
        Add("存储帐户", AzureServiceCategoryKey.Iot, "Microsoft.Storage/storageAccounts");
        Add("函数应用", AzureServiceCategoryKey.Iot, "Microsoft.Web/sites");
        Add("流分析作业", AzureServiceCategoryKey.Iot, "Microsoft.StreamAnalytics/streamingJobs");
        Add("事件中心", AzureServiceCategoryKey.Iot, "Microsoft.EventHub/namespaces");
        Add("Azure 数据资源管理器群集", AzureServiceCategoryKey.Iot, "Microsoft.Kusto/clusters");
        Add("Azure 数字孪生", AzureServiceCategoryKey.Iot, "Microsoft.DigitalTwins/digitalTwinsInstances");
        Add("Azure Cosmos DB", AzureServiceCategoryKey.Iot, "Microsoft.DocumentDB/databaseAccounts");
        Add("Azure Synapse Analytics", AzureServiceCategoryKey.Iot, "Microsoft.Synapse/workspaces");
        Add("Logic apps", AzureServiceCategoryKey.Iot, "Microsoft.Logic/workflows");
        Add("Azure Machine Learning", AzureServiceCategoryKey.Iot, "Microsoft.MachineLearningServices/workspaces");
        Add("Azure Maps 帐户", AzureServiceCategoryKey.Iot, "Microsoft.Maps/accounts");
        Add("Azure Maps Creator 资源", AzureServiceCategoryKey.Iot);
        Add("Power Platform", AzureServiceCategoryKey.Iot);
        Add("Azure Stack Edge / Data Box Gateway", AzureServiceCategoryKey.Iot);

        // ----------------------------------------------------- 监视器 (25)
        Add("更改分析", AzureServiceCategoryKey.Monitor, "Microsoft.ChangeAnalysis/instances");
        Add("警报", AzureServiceCategoryKey.Monitor, "Microsoft.Insights/metricAlerts");
        Add("可观测性智能体", AzureServiceCategoryKey.Monitor);
        Add("托管的 Prometheus", AzureServiceCategoryKey.Monitor, "Microsoft.Monitor/accounts");
        Add("诊断设置", AzureServiceCategoryKey.Monitor);
        Add("指标", AzureServiceCategoryKey.Monitor);
        Add("自动缩放", AzureServiceCategoryKey.Monitor, "Microsoft.Insights/autoscaleSettings");
        Add("Log Analytics 工作区", AzureServiceCategoryKey.Monitor, "Microsoft.OperationalInsights/workspaces");
        Add("Log Analytics 专用群集", AzureServiceCategoryKey.Monitor, "Microsoft.OperationalInsights/clusters");
        Add("Azure Monitor 工作区", AzureServiceCategoryKey.Monitor, "Microsoft.Monitor/accounts");
        Add("Azure Native Dynatrace Service", AzureServiceCategoryKey.Monitor);
        Add("Azure Workbooks", AzureServiceCategoryKey.Monitor, "Microsoft.Insights/workbooks");
        Add("Application Insights", AzureServiceCategoryKey.Monitor, "Microsoft.Insights/components");
        Add("弹性云(Elasticsearch)", AzureServiceCategoryKey.Monitor);
        Add("服务运行状况", AzureServiceCategoryKey.Monitor);
        Add("活动日志", AzureServiceCategoryKey.Monitor);
        Add("监视", AzureServiceCategoryKey.Monitor);
        Add("数据库观察程序数", AzureServiceCategoryKey.Monitor);
        Add("数据收集规则", AzureServiceCategoryKey.Monitor, "Microsoft.Insights/dataCollectionRules");
        Add("数据收集终结点", AzureServiceCategoryKey.Monitor, "Microsoft.Insights/dataCollectionEndpoints");
        Add("Azure 托管 Grafana", AzureServiceCategoryKey.Monitor, "Microsoft.Dashboard/grafana");
        Add("Datadog - Azure Native ISV 服务", AzureServiceCategoryKey.Monitor);
        Add("Log Analytics 查询包", AzureServiceCategoryKey.Monitor);
        Add("Azure Monitors for SAP Solutions", AzureServiceCategoryKey.Monitor);
        Add("Azure Native New Relic Service", AzureServiceCategoryKey.Monitor);

        // ----------------------------------------------------- 网络 (27)
        Add("公共 IP 前缀", AzureServiceCategoryKey.Network, "Microsoft.Network/publicIPPrefixes");
        Add("网络管理器", AzureServiceCategoryKey.Network, "Microsoft.Network/networkManagers");
        Add("专用 DNS 区域", AzureServiceCategoryKey.Network, "Microsoft.Network/privateDnsZones");
        Add("专用链接", AzureServiceCategoryKey.Network, "Microsoft.Network/privateLinkServices");
        Add("自定义 IP 前缀", AzureServiceCategoryKey.Network, "Microsoft.Network/customIPPrefixes");
        Add("Bastions", AzureServiceCategoryKey.Network, "Microsoft.Network/bastionHosts");
        Add("DNS 区域", AzureServiceCategoryKey.Network, "Microsoft.Network/dnsZones");
        Add("DNS 专用解析程序", AzureServiceCategoryKey.Network, "Microsoft.Network/dnsResolvers");
        Add("NAT 网关", AzureServiceCategoryKey.Network, "Microsoft.Network/natGateways");
        Add("Connections", AzureServiceCategoryKey.Network, "Microsoft.Network/connections");
        Add("ExpressRoute circuits", AzureServiceCategoryKey.Network, "Microsoft.Network/expressRouteCircuits");
        Add("ExpressRoute traffic collectors", AzureServiceCategoryKey.Network, "Microsoft.Network/expressRouteTrafficCollectors");
        Add("Local network gateways", AzureServiceCategoryKey.Network, "Microsoft.Network/localNetworkGateways");
        Add("Peering Services", AzureServiceCategoryKey.Network, "Microsoft.Network/peeringServices");
        Add("Peerings", AzureServiceCategoryKey.Network, "Microsoft.Network/peerings");
        Add("Virtual network gateways", AzureServiceCategoryKey.Network, "Microsoft.Network/virtualNetworkGateways");
        Add("Virtual WANs", AzureServiceCategoryKey.Network, "Microsoft.Network/virtualWans");
        Add("负载均衡和内容交付", AzureServiceCategoryKey.Network);
        Add("网络基础", AzureServiceCategoryKey.Network);
        Add("网络监视和管理", AzureServiceCategoryKey.Network);
        Add("DDoS 防护计划", AzureServiceCategoryKey.Network, "Microsoft.Network/ddosProtectionPlans");
        Add("Firewalls", AzureServiceCategoryKey.Network, "Microsoft.Network/azureFirewalls");
        Add("IP Groups", AzureServiceCategoryKey.Network, "Microsoft.Network/ipGroups");
        Add("Web 应用程序防火墙策略(WAF)", AzureServiceCategoryKey.Network, "Microsoft.Network/applicationGatewayWebApplicationFirewallPolicies");
        Add("Microsoft Connected Cache for Internet Service Providers", AzureServiceCategoryKey.Network);
        Add("监视", AzureServiceCategoryKey.Network);
        Add("NGINXaaS", AzureServiceCategoryKey.Network);

        // ----------------------------------------------------- 迁移 (5)
        Add("Azure Database Migration Services", AzureServiceCategoryKey.Migration, "Microsoft.DataMigration/services");
        Add("Azure Migrate", AzureServiceCategoryKey.Migration, "Microsoft.Migrate/migrateProjects");
        Add("Azure Data Box", AzureServiceCategoryKey.Migration);
        Add("Recovery Services 保管库", AzureServiceCategoryKey.Migration, "Microsoft.RecoveryServices/vaults");
        Add("Azure Stack Edge / Data Box Gateway", AzureServiceCategoryKey.Migration);

        // ----------------------------------------------------- 集成 (15)
        Add("应用程序配置", AzureServiceCategoryKey.Integration, "Microsoft.AppConfiguration/configurationStores");
        Add("Integration accounts", AzureServiceCategoryKey.Integration, "Microsoft.Logic/integrationAccounts");
        Add("Logic apps", AzureServiceCategoryKey.Integration, "Microsoft.Logic/workflows");
        Add("Logic Apps Custom Connector", AzureServiceCategoryKey.Integration, "Microsoft.Web/customApis");
        Add("API 管理服务", AzureServiceCategoryKey.Integration, "Microsoft.ApiManagement/service");
        Add("API connections", AzureServiceCategoryKey.Integration);
        Add("Azure API for FHIR", AzureServiceCategoryKey.Integration);
        Add("FHIR 服务", AzureServiceCategoryKey.Integration);
        Add("Health Data Services 工作区", AzureServiceCategoryKey.Integration);
        Add("服务总线", AzureServiceCategoryKey.Integration, "Microsoft.ServiceBus/namespaces");
        Add("事件网格", AzureServiceCategoryKey.Integration, "Microsoft.EventGrid/topics");
        Add("事件中心", AzureServiceCategoryKey.Integration, "Microsoft.EventHub/namespaces");
        Add("事件中心群集", AzureServiceCategoryKey.Integration, "Microsoft.EventHub/clusters");
        Add("中继", AzureServiceCategoryKey.Integration, "Microsoft.Relay/namespaces");
        Add("Apache Kafka & Apache Flink on Confluent Cloud", AzureServiceCategoryKey.Integration);

        return list;
    }
}
