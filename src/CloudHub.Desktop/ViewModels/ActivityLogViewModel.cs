using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CloudHub.Core.Models;
using CloudHub.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CloudHub.Desktop.Services;

namespace CloudHub.Desktop.ViewModels;

public sealed partial class ActivityLogViewModel : PageViewModel
{
    public ActivityLogViewModel(AppServices services) : base(services)
    {
    }

    public override string Title => "活动日志";

    public IReadOnlyList<(string Label, TimeSpan Span)> Ranges { get; } = new[]
    {
        ("过去 24 小时", TimeSpan.FromHours(24)),
        ("过去 7 天", TimeSpan.FromDays(7)),
        ("过去 30 天", TimeSpan.FromDays(30)),
    };

    [ObservableProperty] private TimeSpan _selectedSpan = TimeSpan.FromDays(7);

    [ObservableProperty] private IReadOnlyList<ActivityEvent> _items = System.Array.Empty<ActivityEvent>();

    partial void OnSelectedSpanChanged(TimeSpan value) => _ = LoadAsync();

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
            Items = await Services.ActivityLog.ListAsync(subId, SelectedSpan, 300, ct);
            EmptyHint = Items.Count == 0 ? "这段时间没有活动记录。" : null;
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
