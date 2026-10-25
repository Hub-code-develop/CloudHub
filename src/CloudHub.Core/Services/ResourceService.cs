using CloudHub.Core.Arm;
using CloudHub.Core.Models;

namespace CloudHub.Core.Services;

/// <summary>通用资源查询 —— Azure 门户里那个「所有资源」页面的真实后端。</summary>
public sealed class ResourceService(ArmClient arm)
{
    private const string ApiVersion = "2021-04-01";

    private readonly ArmClient _arm = arm;

    /// <summary>
    /// 列出订阅内所有资源。<paramref name="odataFilter"/> 直接透传给 ARM 的 $filter，
    /// 例如 <c>resourceGroup eq 'rg-dev'</c> 或 <c>resourceType eq 'Microsoft.Storage/storageAccounts'</c>。
    /// </summary>
    public async Task<IReadOnlyList<ArmResource>> ListAsync(
        string subscriptionId,
        string? odataFilter = null,
        CancellationToken ct = default)
    {
        var url = $"/subscriptions/{subscriptionId}/resources?api-version={ApiVersion}";
        if (!string.IsNullOrWhiteSpace(odataFilter))
        {
            url += "&$filter=" + Uri.EscapeDataString(odataFilter);
        }

        var items = await _arm.GetAllPagedAsync(url, ct).ConfigureAwait(false);

        return items
            .Select(ArmResource.FromJson)
            .Where(r => !string.IsNullOrEmpty(r.Id))
            .ToList();
    }

    /// <summary>取单个资源的完整定义（含 properties）。</summary>
    public async Task<ArmResource> GetAsync(string resourceId, CancellationToken ct = default)
    {
        var response = await _arm.GetAsync(
            $"{resourceId}?api-version={ApiVersion}", ct).ConfigureAwait(false);

        return ArmResource.FromJson(response.Body!.Value);
    }

    public async Task DeleteAsync(string resourceId, string? apiVersion = null, CancellationToken ct = default)
    {
        var response = await _arm.DeleteAsync(
            $"{resourceId}?api-version={apiVersion ?? ApiVersion}", ct).ConfigureAwait(false);

        await _arm.WaitForOperationAsync(response, ct).ConfigureAwait(false);
    }

    /// <summary>把资源按 provider 聚合，用于概览页的分布统计。</summary>
    public static IReadOnlyList<(string Provider, int Count)> GroupByProvider(IEnumerable<ArmResource> resources) =>
        resources
            .GroupBy(r => r.Provider, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Provider: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

    public static IReadOnlyList<(string Type, int Count)> GroupByType(IEnumerable<ArmResource> resources, int take = 12) =>
        resources
            .GroupBy(r => r.Type, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Type: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(take)
            .ToList();

    public static IReadOnlyList<(string Location, int Count)> GroupByLocation(IEnumerable<ArmResource> resources) =>
        resources
            .GroupBy(r => r.LocationOrGlobal, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Location: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
}
