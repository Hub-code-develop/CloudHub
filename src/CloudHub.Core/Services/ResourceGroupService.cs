using CloudHub.Core.Arm;
using CloudHub.Core.Models;

namespace CloudHub.Core.Services;

/// <summary>资源组：列举 / 创建 / 删除 / 打标签。</summary>
public sealed class ResourceGroupService(ArmClient arm)
{
    private const string ApiVersion = "2021-04-01";

    private readonly ArmClient _arm = arm;

    public async Task<IReadOnlyList<ResourceGroup>> ListAsync(string subscriptionId, CancellationToken ct = default)
    {
        var items = await _arm.GetAllPagedAsync(
            $"/subscriptions/{subscriptionId}/resourcegroups?api-version={ApiVersion}", ct).ConfigureAwait(false);

        return items
            .Select(ResourceGroup.FromJson)
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<ResourceGroup> GetAsync(string subscriptionId, string name, CancellationToken ct = default)
    {
        var response = await _arm.GetAsync(
            $"/subscriptions/{subscriptionId}/resourcegroups/{Uri.EscapeDataString(name)}?api-version={ApiVersion}", ct)
            .ConfigureAwait(false);

        return ResourceGroup.FromJson(response.Body!.Value);
    }

    public async Task<ResourceGroup> CreateOrUpdateAsync(
        string subscriptionId,
        string name,
        string location,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["location"] = location };
        if (tags is { Count: > 0 })
        {
            body["tags"] = tags;
        }

        var response = await _arm.PutAsync(
            $"/subscriptions/{subscriptionId}/resourcegroups/{Uri.EscapeDataString(name)}?api-version={ApiVersion}",
            body, ct).ConfigureAwait(false);

        var final = await _arm.WaitForOperationAsync(response, ct).ConfigureAwait(false);
        if (final is { } element && element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            return ResourceGroup.FromJson(element);
        }

        // 有些路径下 ARM 直接返回 200 + 资源体，没体时回读一次。
        return await GetAsync(subscriptionId, name, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string subscriptionId, string name, CancellationToken ct = default)
    {
        var response = await _arm.DeleteAsync(
            $"/subscriptions/{subscriptionId}/resourcegroups/{Uri.EscapeDataString(name)}?api-version={ApiVersion}", ct)
            .ConfigureAwait(false);

        await _arm.WaitForOperationAsync(response, ct).ConfigureAwait(false);
    }

    /// <summary>资源组里有多少资源 —— 用一次带过滤的资源查询拿总数。</summary>
    public async Task<int> CountResourcesAsync(string subscriptionId, string name, CancellationToken ct = default)
    {
        var all = await _arm.GetAllPagedAsync(
            $"/subscriptions/{subscriptionId}/resources?api-version=2021-04-01&$filter=resourceGroup eq '{Uri.EscapeDataString(name)}'",
            ct).ConfigureAwait(false);

        return all.Count;
    }
}
