using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CloudHub.Desktop.Services;
using FluentAvalonia.UI.Controls;

namespace CloudHub.Desktop.ViewModels;

/// <summary>所有页面 VM 的基类：持有服务容器、忙碌态、错误与空态提示。</summary>
public abstract partial class PageViewModel : ObservableObject
{
    protected PageViewModel(AppServices services)
    {
        Services = services;
    }

    public AppServices Services { get; }

    public virtual string Title => string.Empty;

    public virtual FASymbol Icon => FASymbol.Document;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _emptyHint;

    /// <summary>页面每次被切到前台时调用，默认空实现。</summary>
    public virtual Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>左侧导航条目（纯展示数据）。</summary>
public sealed record NavItem(string Tag, string Title, FASymbol Icon, bool IsFooter = false);
