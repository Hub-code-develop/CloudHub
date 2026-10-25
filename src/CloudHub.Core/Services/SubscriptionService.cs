using System.Text.Json;
using CloudHub.Core.Arm;
using CloudHub.Core.Auth;
using CloudHub.Core.Models;

namespace CloudHub.Core.Services;

/// <summary>订阅与区域。对应 Microsoft.Resources。</summary>
public sealed class SubscriptionService(ArmClient arm, AuthService auth)
{
    private const string ApiVersion = "2022-12-01";

    private readonly ArmClient _arm = arm;
    private readonly AuthService _auth = auth;
    private IReadOnlyList<AzureSubscription>? _cache;

    public async Task<IReadOnlyList<AzureSubscription>> ListAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (_cache is not null && !forceRefresh)
        {
            return _cache;
        }

        var collected = new Dictionary<string, AzureSubscription>(StringComparer.OrdinalIgnoreCase);

        // 标准多租户枚举：先列出租户，再逐租户换 token 拉订阅，聚合去重。
        // 这样挂在不同目录（例如个人 Microsoft 账号所在的个人租户）下的订阅也能被扫到。
        try
        {
            var tenants = await ListTenantsAsync(ct).ConfigureAwait(false);
            foreach (var tenant in tenants)
            {
                if (string.IsNullOrWhiteSpace(tenant))
                {
                    continue;
                }

                try
                {
                    var token = await _auth.GetAccessTokenForTenantAsync(tenant, ct).ConfigureAwait(false);
                    await CollectSubscriptionsAsync(collected, token, ct).ConfigureAwait(false);
                }
                catch
                {
                    // 单个租户无权限 / 兑换失败则跳过，不拖累整体枚举。
                }
            }
        }
        catch
        {
            // 列租户失败（极端情况），下面回退到当前 token 直拉。
        }

        // 回退：逐租户一个都没拿到时，用当前（家庭租户）token 直接拉一次。
        if (collected.Count == 0)
        {
            try
            {
                await CollectSubscriptionsAsync(collected, null, ct).ConfigureAwait(false);
            }
            catch
            {
                // 连直拉都失败就返回空，错误由调用方（订阅页）展示。
            }
        }

        _cache = collected.Values
            .OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return _cache;
    }

    private async Task CollectSubscriptionsAsync(
        Dictionary<string, AzureSubscription> collected, string? token, CancellationToken ct)
    {
        var items = await _arm.GetAllPagedAsync($"/subscriptions?api-version={ApiVersion}", ct: ct, accessToken: token)
            .ConfigureAwait(false);

        foreach (var e in items)
        {
            var sub = AzureSubscription.FromJson(e);
            if (!string.IsNullOrEmpty(sub.Id) && !collected.ContainsKey(sub.Id))
            {
                collected[sub.Id] = sub;
            }
        }
    }

    private async Task<List<string>> ListTenantsAsync(CancellationToken ct)
    {
        var items = await _arm.GetAllPagedAsync("/tenants?api-version=2020-01-01", ct: ct)
            .ConfigureAwait(false);

        var list = new List<string>();
        foreach (var e in items)
        {
            var tid = e.Str("tenantId");
            if (string.IsNullOrWhiteSpace(tid))
            {
                // 兜底：id 形如 /tenants/{guid}
                var id = e.Str("id");
                var idx = id.LastIndexOf('/');
                tid = idx >= 0 ? id[(idx + 1)..] : id;
            }

            if (!string.IsNullOrWhiteSpace(tid) && !list.Contains(tid, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(tid);
            }
        }

        return list;
    }

    public void Invalidate() => _cache = null;

    /// <summary>某订阅下可用的区域列表，用于创建资源组。</summary>
    public async Task<IReadOnlyList<AzureLocation>> ListLocationsAsync(string subscriptionId, CancellationToken ct = default)
    {
        var items = await _arm.GetAllPagedAsync(
            $"/subscriptions/{subscriptionId}/locations?api-version={ApiVersion}", ct).ConfigureAwait(false);

        return items
            .Select(AzureLocation.FromJson)
            .Where(l => !string.IsNullOrEmpty(l.Name))
            .OrderBy(l => l.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
