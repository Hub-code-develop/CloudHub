using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CloudHub.Core.Models;
using CloudHub.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CloudHub.Desktop.Services;

namespace CloudHub.Desktop.ViewModels;

public sealed partial class CostViewModel : PageViewModel
{
    public CostViewModel(AppServices services) : base(services)
    {
    }

    public override string Title => "成本分析";

    public IReadOnlyList<(string Label, CostTimeframe Value)> Timeframes { get; } = new[]
    {
        ("本月至今", CostTimeframe.MonthToDate),
        ("计费月至今", CostTimeframe.BillingMonthToDate),
        ("上月", CostTimeframe.TheLastMonth),
        ("本周至今", CostTimeframe.WeekToDate),
        ("上周", CostTimeframe.TheLastWeek),
    };

    [ObservableProperty] private CostTimeframe _selectedTimeframe = CostTimeframe.MonthToDate;

    [ObservableProperty] private decimal _total;
    [ObservableProperty] private string _currency = "—";
    [ObservableProperty] private IReadOnlyList<CostBreakdownItem> _byService = System.Array.Empty<CostBreakdownItem>();
    [ObservableProperty] private IReadOnlyList<CostBreakdownItem> _byResourceGroup = System.Array.Empty<CostBreakdownItem>();

    partial void OnSelectedTimeframeChanged(CostTimeframe value) => _ = LoadAsync();

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        var subId = Services.SelectedSubscriptionId;
        if (string.IsNullOrEmpty(subId))
        {
            EmptyHint = "请先在左上角选择一个订阅。";
            return;
        }

        EmptyHint = null;
        IsBusy = true;
        Error = null;

        try
        {
            var report = await Services.Costs.QueryAsync(subId, SelectedTimeframe, ct);
            Total = report.Total;
            Currency = report.Currency;
            ByService = report.ByService;
            ByResourceGroup = report.ByResourceGroup;
            EmptyHint = report.IsEmpty ? "这段时间没有产生成本。" : null;
        }
        catch (System.Exception ex)
        {
            Error = Summarize(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Refresh() => _ = LoadAsync();

    private static string Summarize(System.Exception ex)
        => ex is CloudHub.Core.Arm.ArmException ae ? ae.Message : ex.Message;
}
