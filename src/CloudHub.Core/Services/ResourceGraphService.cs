using System.Text.Json;
using CloudHub.Core.Arm;

namespace CloudHub.Core.Services;

/// <summary>Azure Resource Graph 查询结果的一行。</summary>
public sealed record GraphRow(IReadOnlyDictionary<string, string> Values, JsonElement Raw);

public sealed record GraphResult
{
    public IReadOnlyList<string> Columns { get; init; } = [];
    public IReadOnlyList<GraphRow> Rows { get; init; } = [];
    public int TotalRecords { get; init; }
    public bool Truncated { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string Query { get; init; } = string.Empty;
}

/// <summary>
/// Azure Resource Graph —— 门户里藏在「Resource Graph 资源管理器」里的能力。
/// 一条 KQL 就能跨订阅、跨资源组把资源捞出来，比门户的资源列表快一个数量级。
/// </summary>
public sealed class ResourceGraphService(ArmClient arm)
{
    private const string ApiVersion = "2022-10-01";

    private readonly ArmClient _arm = arm;

    public const string DefaultQuery =
        """
        resources
        | project name, type, resourceGroup, location, subscriptionId
        | sort by name asc
        | limit 100
        """;

    /// <summary>跑一条 KQL。subscriptions 为空时默认查当前订阅。</summary>
    public async Task<GraphResult> QueryAsync(
        IReadOnlyList<string> subscriptions,
        string query,
        int top = 200,
        CancellationToken ct = default)
    {
        var body = new
        {
            subscriptions,
            query,
            options = new
            {
                resultFormat = "objectArray",
                top,
                skip = 0,
            },
        };

        var started = System.Diagnostics.Stopwatch.StartNew();
        var response = await _arm.PostAsync(
            $"/providers/Microsoft.ResourceGraph/resources?api-version={ApiVersion}", body, ct).ConfigureAwait(false);
        started.Stop();

        return Parse(response.Body, query, started.Elapsed);
    }

    /// <summary>全局搜索：按名称 / 类型 / 资源组模糊匹配。</summary>
    public Task<GraphResult> SearchAsync(
        IReadOnlyList<string> subscriptions,
        string keyword,
        int top = 40,
        CancellationToken ct = default)
    {
        var escaped = keyword.Replace("\\", "\\\\").Replace("'", "\\'");
        var query =
            $"""
             resources
             | where name contains '{escaped}' or type contains '{escaped}' or resourceGroup contains '{escaped}'
             | project name, type, resourceGroup, location, subscriptionId, id
             | sort by name asc
             | limit {top}
             """;

        return QueryAsync(subscriptions, query, top, ct);
    }

    private static GraphResult Parse(JsonElement? body, string query, TimeSpan elapsed)
    {
        if (body is not { } root || root.ValueKind != JsonValueKind.Object)
        {
            return new GraphResult { Query = query, Elapsed = elapsed };
        }

        var totalRecords = root.TryGetProperty("totalRecords", out var tr) && tr.TryGetInt32(out var t) ? t : 0;
        var truncated = root.TryGetProperty("resultTruncated", out var rt)
                        && rt.ValueKind == JsonValueKind.String
                        && rt.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        var rows = new List<GraphRow>();
        var columns = new List<string>();

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in item.EnumerateObject())
                {
                    if (!columns.Contains(property.Name))
                    {
                        columns.Add(property.Name);
                    }

                    values[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                        JsonValueKind.Null => string.Empty,
                        JsonValueKind.Object or JsonValueKind.Array => property.Value.ToString(),
                        _ => property.Value.ToString(),
                    };
                }

                rows.Add(new GraphRow(values, item.Clone()));
            }
        }

        if (totalRecords == 0)
        {
            totalRecords = rows.Count;
        }

        return new GraphResult
        {
            Columns = columns,
            Rows = rows,
            TotalRecords = totalRecords,
            Truncated = truncated,
            Elapsed = elapsed,
            Query = query,
        };
    }
}
