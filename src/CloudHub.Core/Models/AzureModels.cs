using System.Globalization;
using System.Text.Json;

namespace CloudHub.Core.Models;

internal static class JsonHelp
{
    public static string Str(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    public static string? StrOrNull(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static JsonElement? Obj(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    public static IReadOnlyList<JsonElement> Arr(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(x => x.Clone()).ToList()
            : [];

    public static IReadOnlyDictionary<string, string> Tags(this JsonElement e)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (e.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
        {
            foreach (var tag in tags.EnumerateObject())
            {
                result[tag.Name] = tag.Value.ValueKind == JsonValueKind.String
                    ? tag.Value.GetString() ?? string.Empty
                    : tag.Value.ToString();
            }
        }

        return result;
    }

    /// <summary>从 /subscriptions/{sub}/resourceGroups/{rg}/providers/... 里抠出订阅与资源组名。</summary>
    public static (string SubscriptionId, string ResourceGroupName) SplitResourceId(string id)
    {
        var parts = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sub = string.Empty;
        var rg = string.Empty;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("subscriptions", StringComparison.OrdinalIgnoreCase))
            {
                sub = parts[i + 1];
            }
            else if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                rg = parts[i + 1];
            }
        }

        return (sub, rg);
    }
}

/// <summary>Azure 订阅。</summary>
public sealed record AzureSubscription
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string State { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string? OfferId { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    public bool IsEnabled => State.Equals("Enabled", StringComparison.OrdinalIgnoreCase);

    public string ShortId => Id.Length >= 8 ? Id[..8] : Id;

    public static AzureSubscription FromJson(JsonElement e)
    {
        var props = e.Obj("properties");
        return new AzureSubscription
        {
            Id = e.Str("id"),
            DisplayName = e.Str("displayName") is { Length: > 0 } n ? n : e.Str("id"),
            State = e.Str("state"),
            TenantId = e.Str("tenantId"),
            OfferId = props?.StrOrNull("offerId"),
            Tags = e.Tags(),
        };
    }
}

/// <summary>资源组。</summary>
public sealed record ResourceGroup
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Location { get; init; } = string.Empty;
    public string SubscriptionId { get; init; } = string.Empty;
    public string ProvisioningState { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    public string TagSummary => Tags.Count == 0 ? "—" : string.Join(", ", Tags.Select(t => $"{t.Key}={t.Value}"));

    public static ResourceGroup FromJson(JsonElement e)
    {
        var id = e.Str("id");
        return new ResourceGroup
        {
            Id = id,
            Name = e.Str("name"),
            Location = e.Str("location"),
            SubscriptionId = e.StrOrNull("subscriptionId") ?? JsonHelp.SplitResourceId(id).SubscriptionId,
            ProvisioningState = e.Obj("properties")?.StrOrNull("provisioningState") ?? string.Empty,
            Tags = e.Tags(),
        };
    }

    /// <summary>分析资源组后返回的最大容量说明（0-10 为「已满」）。</summary>
    public static int CapacityTier(int resourceCount) => resourceCount switch
    {
        < 100 => 1,
        < 300 => 2,
        < 600 => 3,
        < 800 => 4,
        _ => 5,
    };
}

/// <summary>ARM 里的一条通用资源。保留 Raw 以便详情页展示完整 JSON。</summary>
public sealed record ArmResource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string SubscriptionId { get; init; } = string.Empty;
    public string ResourceGroupName { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
    public JsonElement Raw { get; init; }

    /// <summary>例如 Microsoft.Compute。</summary>
    public string Provider =>
        Type.IndexOf('/') is var i && i > 0 ? Type[..i] : Type;

    /// <summary>例如 virtualMachines。</summary>
    public string ShortType =>
        Type.LastIndexOf('/') is var i && i >= 0 && i < Type.Length - 1 ? Type[(i + 1)..] : Type;

    public bool IsGlobal => string.IsNullOrEmpty(Location) || Location.Equals("global", StringComparison.OrdinalIgnoreCase);

    public string LocationOrGlobal => IsGlobal ? "global" : Location;

    public static ArmResource FromJson(JsonElement e)
    {
        var id = e.Str("id");
        var (sub, rg) = JsonHelp.SplitResourceId(id);

        return new ArmResource
        {
            Id = id,
            Name = e.Str("name"),
            Type = e.Str("type"),
            Location = e.Str("location"),
            Kind = e.Str("kind"),
            SubscriptionId = sub,
            ResourceGroupName = rg,
            Tags = e.Tags(),
            Raw = e.Clone(),
        };
    }
}

/// <summary>虚拟机电源状态。</summary>
public sealed record VmPowerState(string Code, string Display)
{
    public static readonly VmPowerState Unknown = new("unknown", "未知");

    public bool IsRunning => Code.Equals("running", StringComparison.OrdinalIgnoreCase);

    public bool IsDeallocated => Code.Equals("deallocated", StringComparison.OrdinalIgnoreCase);

    public bool IsStopped => Code.Equals("stopped", StringComparison.OrdinalIgnoreCase);

    /// <summary>处于稳定态、可以再次下发电源指令。</summary>
    public bool IsSettled => IsRunning || IsDeallocated || IsStopped;

    public static VmPowerState FromStatuses(IReadOnlyList<JsonElement> statuses)
    {
        foreach (var status in statuses)
        {
            var code = status.Str("code");
            if (code.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase))
            {
                var bare = code["PowerState/".Length..];
                return new VmPowerState(bare, (status.StrOrNull("displayStatus") ?? bare) switch
                {
                    "VM running" => "运行中",
                    "VM deallocated" => "已解除分配",
                    "VM stopped" => "已停止",
                    "VM starting" => "启动中",
                    "VM stopping" => "停止中",
                    "VM deallocating" => "解除分配中",
                    "VM restarting" => "重启中",
                    var other => other,
                });
            }
        }

        return Unknown;
    }
}

/// <summary>虚拟机。</summary>
public sealed record VirtualMachine
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Location { get; init; } = string.Empty;
    public string SubscriptionId { get; init; } = string.Empty;
    public string ResourceGroupName { get; init; } = string.Empty;
    public string VmSize { get; init; } = string.Empty;
    public string OsType { get; init; } = string.Empty;
    public string ProvisioningState { get; init; } = string.Empty;
    public VmPowerState PowerState { get; init; } = VmPowerState.Unknown;
    public int? VcpuCount { get; init; }
    public int? MemoryMb { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    public string SizeLabel => string.IsNullOrEmpty(VmSize) ? "未知规格" : VmSize;

    public string SpecLabel => VcpuCount is > 0 && MemoryMb is > 0
        ? $"{VcpuCount} vCPU · {MemoryMb / 1024.0:0.#} GiB"
        : "—";

    public static VirtualMachine FromJson(JsonElement e)
    {
        var id = e.Str("id");
        var (sub, rg) = JsonHelp.SplitResourceId(id);
        var props = e.Obj("properties");
        var hardware = props?.Obj("hardwareProfile");
        var storage = props?.Obj("storageProfile")?.Obj("osDisk");

        var statuses = props?.Obj("instanceView")?.Arr("statuses") ?? [];

        // vmSize 可能在 properties.hardwareProfile.vmSize，少数接口放在 properties.vmSize。
        var vmSize = hardware?.StrOrNull("vmSize")
                     ?? props?.StrOrNull("vmSize")
                     ?? string.Empty;

        return new VirtualMachine
        {
            Id = id,
            Name = e.Str("name"),
            Location = e.Str("location"),
            SubscriptionId = sub,
            ResourceGroupName = rg,
            VmSize = vmSize,
            OsType = storage?.StrOrNull("osType") ?? string.Empty,
            ProvisioningState = props?.StrOrNull("provisioningState") ?? string.Empty,
            PowerState = VmPowerState.FromStatuses(statuses),
            Tags = e.Tags(),
        };
    }

    /// <summary>把 SKU 名映射到大致规格（仅限常见通用型号，取不到就留空，不编造）。</summary>
    public static (int? Vcpu, int? MemoryMb) EstimateSku(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return (null, null);
        }

        // Azure 规格命名规范：[Family][vCPUs][约束]*[AdditiveFeatures]_[版本]
        var match = System.Text.RegularExpressions.Regex.Match(sku, @"^[A-Za-z]+(\d+)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var vcpu))
        {
            return (null, null);
        }

        // 前导 Family 里的字母个数决定每 vCPU 的内存（D=4GiB, E=8GiB, F=2GiB, B=0.5GiB, 其他按 4GiB）。
        var family = match.Value[..^match.Groups[1].Value.Length];
        var gibPerVcpu = family.StartsWith('E') ? 8
            : family.StartsWith('F') ? 2
            : family.StartsWith('B') ? 0.5
            : family.StartsWith('A') ? 2
            : family.StartsWith('M') ? 16
            : family.StartsWith('L') ? 8
            : 4;

        return (vcpu, (int)(vcpu * gibPerVcpu * 1024));
    }
}

/// <summary>成本查询的一行。</summary>
public sealed record CostRow(string ServiceName, string ResourceGroup, decimal Cost, string Currency);

public sealed record CostReport
{
    public IReadOnlyList<CostRow> Rows { get; init; } = [];
    public decimal Total { get; init; }
    public string Currency { get; init; } = "CNY";
    public string Timeframe { get; init; } = string.Empty;
    public IReadOnlyList<CostBreakdownItem> ByService { get; init; } = [];
    public IReadOnlyList<CostBreakdownItem> ByResourceGroup { get; init; } = [];
    public bool IsEmpty => Rows.Count == 0;
}

public sealed record CostBreakdownItem(string Name, decimal Cost, double Share);

/// <summary>活动日志事件。</summary>
public sealed record ActivityEvent
{
    public DateTimeOffset Timestamp { get; init; }
    public string OperationName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Caller { get; init; }
    public string? ResourceId { get; init; }
    public string? ResourceGroupName { get; init; }
    public string? Description { get; init; }
    public string? CorrelationId { get; init; }

    public bool IsFailure => Status.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                             || Level.Equals("Error", StringComparison.OrdinalIgnoreCase);

    public bool IsWarning => Level.Equals("Warning", StringComparison.OrdinalIgnoreCase);

    public string TimeLabel => Timestamp.ToLocalTime().ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static ActivityEvent FromJson(JsonElement e)
    {
        var resourceId = e.StrOrNull("resourceId");
        var (_, rg) = JsonHelp.SplitResourceId(resourceId ?? string.Empty);

        DateTimeOffset.TryParse(e.StrOrNull("eventTimestamp"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts);

        return new ActivityEvent
        {
            Timestamp = ts,
            OperationName = e.Obj("operationName") is { } op
                ? op.StrOrNull("localizedValue") ?? op.Str("value")
                : string.Empty,
            Status = e.Obj("status")?.Str("value") ?? string.Empty,
            Level = e.Str("level"),
            Category = e.Obj("category")?.Str("value") ?? string.Empty,
            Caller = e.StrOrNull("caller"),
            ResourceId = resourceId,
            ResourceGroupName = string.IsNullOrEmpty(rg) ? null : rg,
            Description = e.StrOrNull("description"),
            CorrelationId = e.StrOrNull("correlationId"),
        };
    }
}

/// <summary>一处 Azure 区域（用于创建资源组时选择位置）。</summary>
public sealed record AzureLocation(string Name, string DisplayName)
{
    public string Label => string.IsNullOrEmpty(DisplayName) ? Name : $"{DisplayName} ({Name})";

    public static AzureLocation FromJson(JsonElement e)
    {
        var name = e.Str("name");
        var display = e.StrOrNull("displayName");
        if (string.IsNullOrWhiteSpace(display))
        {
            display = e.Obj("metadata")?.StrOrNull("displayName");
        }

        return new AzureLocation(name, display ?? name);
    }
}
