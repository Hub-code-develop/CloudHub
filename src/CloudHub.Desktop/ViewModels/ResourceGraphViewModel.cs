using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudHub.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CloudHub.Desktop.Services;

namespace CloudHub.Desktop.ViewModels;

public sealed partial class ResourceGraphViewModel : PageViewModel
{
    public ResourceGraphViewModel(AppServices services) : base(services)
    {
        Query = ResourceGraphService.DefaultQuery;
    }

    public override string Title => "资源图谱";

    [ObservableProperty] private string _query = string.Empty;

    [ObservableProperty] private IReadOnlyList<string> _columns = System.Array.Empty<string>();

    [ObservableProperty] private IReadOnlyList<IReadOnlyList<string>> _rows = System.Array.Empty<IReadOnlyList<string>>();

    [ObservableProperty] private int _totalRecords;

    [ObservableProperty] private string _elapsed = "—";

    [ObservableProperty] private bool _truncated;

    [ObservableProperty] private int _resultCount;

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        // 进入页面自动跑一次默认查询，方便首次接触。
        if (string.IsNullOrWhiteSpace(Query))
        {
            Query = ResourceGraphService.DefaultQuery;
        }

        await RunAsync();
    }

    [RelayCommand]
    private async Task RunAsync()
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
            var result = await Services.ResourceGraph.QueryAsync(
                new[] { subId }, Query, 200, CancellationToken.None);

            Columns = result.Columns;
            Rows = result.Rows
                .Select(r => Columns.Select(c => r.Values.TryGetValue(c, out var v) ? v : string.Empty).ToList())
                .ToList();
            TotalRecords = result.TotalRecords;
            Elapsed = $"{result.Elapsed.TotalMilliseconds:0} ms";
            Truncated = result.Truncated;
            ResultCount = result.Rows.Count;
            EmptyHint = result.Rows.Count == 0 ? "查询没有返回任何行。" : null;
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

    private static string Summarize(System.Exception ex)
        => ex is CloudHub.Core.Arm.ArmException ae ? ae.Message : ex.Message;
}
