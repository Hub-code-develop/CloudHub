using System.Globalization;
using System.Text.Json;
using CloudHub.Core.Arm;
using CloudHub.Core.Models;

namespace CloudHub.Core.Services;

public enum CostTimeframe
{
    MonthToDate,
    BillingMonthToDate,
    TheLastMonth,
    WeekToDate,
    TheLastWeek,
}

/// <summary>Azure 成本管理查询（Microsoft.CostManagement/query）。</summary>
public sealed class CostService(ArmClient arm)
{
    private const string ApiVersion = "2023-11-01";

    private readonly ArmClient _arm = arm;

    public static string ToApiValue(CostTimeframe timeframe) => timeframe switch
    {
        CostTimeframe.MonthToDate => "MonthToDate",
        CostTimeframe.BillingMonthToDate => "BillingMonthToDate",
        CostTimeframe.TheLastMonth => "TheLastMonth",
        CostTimeframe.WeekToDate => "WeekToDate",
        CostTimeframe.TheLastWeek => "TheLastWeek",
        _ => "MonthToDate",
    };

    /// <summary>按「服务」和「资源组」两个维度把实际成本聚合出来。</summary>
    public async Task<CostReport> QueryAsync(
        string subscriptionId,
        CostTimeframe timeframe = CostTimeframe.MonthToDate,
        CancellationToken ct = default)
    {
        var body = new
        {
            type = "ActualCost",
            timeframe = ToApiValue(timeframe),
            dataset = new
            {
                granularity = "None",
                aggregation = new
                {
                    totalCost = new { name = "PreTaxCost", function = "Sum" },
                },
                grouping = new object[]
                {
                    new { type = "Dimension", name = "ServiceName" },
                    new { type = "Dimension", name = "ResourceGroupName" },
                },
            },
        };

        var response = await _arm.PostAsync(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version={ApiVersion}",
            body, ct).ConfigureAwait(false);

        return Parse(response.Body, ToApiValue(timeframe));
    }

    private static CostReport Parse(JsonElement? body, string timeframe)
    {
        if (body is not { } root
            || !root.TryGetProperty("properties", out var props)
            || !props.TryGetProperty("columns", out var columnsElement)
            || !props.TryGetProperty("rows", out var rowsElement)
            || columnsElement.ValueKind != JsonValueKind.Array
            || rowsElement.ValueKind != JsonValueKind.Array)
        {
            return new CostReport { Timeframe = timeframe };
        }

        var columns = columnsElement.EnumerateArray()
            .Select(c => c.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty)
            .ToList();

        var costIndex = columns.FindIndex(c => c.Equals("PreTaxCost", StringComparison.OrdinalIgnoreCase));
        var currencyIndex = columns.FindIndex(c => c.Equals("Currency", StringComparison.OrdinalIgnoreCase));
        var serviceIndex = columns.FindIndex(c => c.Equals("ServiceName", StringComparison.OrdinalIgnoreCase));
        var rgIndex = columns.FindIndex(c => c.Equals("ResourceGroupName", StringComparison.OrdinalIgnoreCase));

        var rows = new List<CostRow>();
        var currency = "CNY";

        foreach (var row in rowsElement.EnumerateArray())
        {
            var values = row.EnumerateArray().ToList();
            if (values.Count != columns.Count)
            {
                continue;
            }

            var cost = ReadDecimal(values, costIndex);
            var rowCurrency = currencyIndex >= 0 ? values[currencyIndex].ToString() : string.Empty;
            if (!string.IsNullOrWhiteSpace(rowCurrency))
            {
                currency = rowCurrency;
            }

            rows.Add(new CostRow(
                ReadString(values, serviceIndex, "未归类服务"),
                ReadString(values, rgIndex, "—"),
                cost,
                currency));
        }

        // 有些分组组合服务端会返回 0 成本的行，直接剔掉免得图表全是空条。
        var meaningful = rows.Where(r => r.Cost != 0).ToList();

        var byService = meaningful
            .GroupBy(r => r.ServiceName, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new { Name = g.Key, Cost = g.Sum(x => x.Cost) })
            .OrderByDescending(x => x.Cost)
            .ToList();

        var byResourceGroup = meaningful
            .GroupBy(r => r.ResourceGroup, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new { Name = g.Key, Cost = g.Sum(x => x.Cost) })
            .OrderByDescending(x => x.Cost)
            .ToList();

        var total = meaningful.Sum(r => r.Cost);

        return new CostReport
        {
            Rows = meaningful,
            Total = total,
            Currency = currency,
            Timeframe = timeframe,
            ByService = byService
                .Select(x => new CostBreakdownItem(x.Name, x.Cost, total == 0 ? 0 : (double)(x.Cost / total)))
                .ToList(),
            ByResourceGroup = byResourceGroup
                .Select(x => new CostBreakdownItem(x.Name, x.Cost, total == 0 ? 0 : (double)(x.Cost / total)))
                .ToList(),
        };
    }

    private static decimal ReadDecimal(IReadOnlyList<JsonElement> values, int index)
    {
        if (index < 0 || index >= values.Count)
        {
            return 0m;
        }

        var element = values[index];
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => 0m,
        };
    }

    private static string ReadString(IReadOnlyList<JsonElement> values, int index, string fallback)
    {
        if (index < 0 || index >= values.Count)
        {
            return fallback;
        }

        var text = values[index].ToString();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}

/// <summary>活动日志（Microsoft.Insights eventtypes/management）。</summary>
public sealed class ActivityLogService(ArmClient arm)
{
    private const string ApiVersion = "2015-04-01";

    private readonly ArmClient _arm = arm;

    public async Task<IReadOnlyList<ActivityEvent>> ListAsync(
        string subscriptionId,
        TimeSpan lookback,
        int take = 200,
        CancellationToken ct = default)
    {
        var to = DateTimeOffset.UtcNow;
        var from = to - lookback;

        var filter =
            $"eventTimestamp ge '{from:yyyy-MM-ddTHH:mm:ssZ}' and eventTimestamp le '{to:yyyy-MM-ddTHH:mm:ssZ}'";

        var url = $"/subscriptions/{subscriptionId}/providers/microsoft.insights/eventtypes/management/values" +
                  $"?api-version={ApiVersion}&$filter={Uri.EscapeDataString(filter)}&$top={take}";

        var items = await _arm.GetAllPagedAsync(url, ct).ConfigureAwait(false);

        return items
            .Select(ActivityEvent.FromJson)
            .Where(e => e.Timestamp != default)
            .OrderByDescending(e => e.Timestamp)
            .Take(take)
            .ToList();
    }
}
