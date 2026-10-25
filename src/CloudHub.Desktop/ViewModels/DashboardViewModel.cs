using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudHub.Core.Models;
using CloudHub.Core.Services;
using CloudHub.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudHub.Desktop.ViewModels;

public sealed record DistributionItem(string Label, int Count, double Share);

public sealed partial class DashboardViewModel : PageViewModel
{
    public DashboardViewModel(AppServices services) : base(services)
    {
    }

    public override string Title => "概览";

    [ObservableProperty] private int _totalResources;
    [ObservableProperty] private int _totalResourceGroups;
    [ObservableProperty] private int _totalVms;
    [ObservableProperty] private int _runningVms;
    [ObservableProperty] private decimal _monthCost;
    [ObservableProperty] private string _costCurrency = "—";
    [ObservableProperty] private int _recentActivity;
    [ObservableProperty] private string _subscriptionName = "—";
    [ObservableProperty] private IReadOnlyList<DistributionItem> _typeDistribution = System.Array.Empty<DistributionItem>();
    [ObservableProperty] private IReadOnlyList<DistributionItem> _locationDistribution = System.Array.Empty<DistributionItem>();

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
            var sub = Services.Subscriptions.ListAsync(ct: ct);
            var resources = Services.Resources.ListAsync(subId, ct: ct);
            var groups = Services.ResourceGroups.ListAsync(subId, ct);
            var vms = Services.VirtualMachines.ListAsync(subId, ct);
            var cost = Services.Costs.QueryAsync(subId, CostTimeframe.MonthToDate, ct);
            var activity = Services.ActivityLog.ListAsync(subId, TimeSpan.FromDays(7), 100, ct);

            await Task.WhenAll(sub, resources, groups, vms, cost, activity);

            var subList = await sub;
            var resourceList = await resources;
            var groupList = await groups;
            var vmList = await vms;
            var report = await cost;
            var events = await activity;

            SubscriptionName = subList.FirstOrDefault(s => s.Id == subId)?.DisplayName ?? subId!;
            TotalResources = resourceList.Count;
            TotalResourceGroups = groupList.Count;
            TotalVms = vmList.Count;
            RunningVms = vmList.Count(v => v.PowerState.IsRunning);
            MonthCost = report.Total;
            CostCurrency = report.Currency;
            RecentActivity = events.Count;

            TypeDistribution = ResourceService.GroupByType(resourceList)
                .Select(x => new DistributionItem(Shorten(x.Type), x.Count, TotalResources == 0 ? 0 : (double)x.Count / TotalResources))
                .ToList();

            LocationDistribution = ResourceService.GroupByLocation(resourceList)
                .Select(x => new DistributionItem(x.Location, x.Count, TotalResources == 0 ? 0 : (double)x.Count / TotalResources))
                .ToList();
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

    private static string Shorten(string type)
    {
        var i = type.LastIndexOf('/');
        return i >= 0 ? type[(i + 1)..] : type;
    }

    private static string Summarize(System.Exception ex)
        => ex is CloudHub.Core.Arm.ArmException ae ? ae.Message : ex.Message;

    [RelayCommand]
    private void Refresh() => _ = LoadAsync();
}
