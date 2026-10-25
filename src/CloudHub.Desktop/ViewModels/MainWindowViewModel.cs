using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CloudHub.Core.Auth;
using CloudHub.Core.Models;
using CloudHub.Core.Services;
using CloudHub.Desktop.Services;
using CloudHub.Desktop.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;

namespace CloudHub.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<string, (PageViewModel Vm, Control View)> _pages = new();
    private readonly ResourcesViewModel _resourcesVm;

    public MainWindowViewModel(AppServices services)
    {
        Services = services;

        NavItems = new List<NavItem>
        {
            new("dashboard", "概览", FASymbol.View),
            new("resources", "资源", FASymbol.World),
            new("resourceGroups", "资源组", FASymbol.Folder),
            new("virtualMachines", "虚拟机", FASymbol.Play),
            new("cost", "成本分析", FASymbol.Calculator),
            new("activity", "活动日志", FASymbol.Clock),
            new("graph", "资源图谱", FASymbol.Code),
            new("subscriptions", "订阅", FASymbol.Globe),
            new("allservices", "所有服务", FASymbol.AllApps),
            new("cat:compute", "计算", FASymbol.Play),
            new("cat:storage", "存储", FASymbol.Save),
            new("cat:networking", "网络", FASymbol.Link),
            new("cat:database", "数据库", FASymbol.Document),
            new("cat:webcontainer", "Web 与容器", FASymbol.Globe),
            new("cat:identity", "安全与身份", FASymbol.People),
            new("cat:ai", "人工智能", FASymbol.Page),
            new("cat:monitor", "监控与集成", FASymbol.ViewAll),
            new("settings", "设置", FASymbol.Settings, IsFooter: true),
        };

        _resourcesVm = new ResourcesViewModel(services);
        RegisterPage("dashboard", new DashboardViewModel(services), new DashboardView());
        RegisterPage("resources", _resourcesVm, new ResourcesView());
        RegisterPage("resourceGroups", new ResourceGroupsViewModel(services), new ResourceGroupsView());
        RegisterPage("virtualMachines", new VirtualMachinesViewModel(services), new VirtualMachinesView());
        RegisterPage("cost", new CostViewModel(services), new CostView());
        RegisterPage("activity", new ActivityLogViewModel(services), new ActivityLogView());
        RegisterPage("graph", new ResourceGraphViewModel(services), new ResourceGraphView());
        RegisterPage("subscriptions", new SubscriptionsViewModel(services), new SubscriptionsView());
        RegisterPage("allservices", new AllServicesViewModel(services), new AllServicesView());

        // 服务族页：复用通用资源浏览器，按 ResourceTypeCatalog 分类预置过滤。
        RegisterPage("cat:compute", new ResourcesViewModel(services, ResourceCategory.Compute), new ResourcesView());
        RegisterPage("cat:storage", new ResourcesViewModel(services, ResourceCategory.Storage), new ResourcesView());
        RegisterPage("cat:networking", new ResourcesViewModel(services, ResourceCategory.Networking), new ResourcesView());
        RegisterPage("cat:database", new ResourcesViewModel(services, ResourceCategory.Database), new ResourcesView());
        RegisterPage("cat:webcontainer", new ResourcesViewModel(services, ResourceCategory.Web, ResourceCategory.Container), new ResourcesView());
        RegisterPage("cat:identity", new ResourcesViewModel(services, ResourceCategory.Identity), new ResourcesView());
        RegisterPage("cat:ai", new ResourcesViewModel(services, ResourceCategory.Ai), new ResourcesView());
        RegisterPage("cat:monitor", new ResourcesViewModel(services, ResourceCategory.Monitoring, ResourceCategory.Integration), new ResourcesView());

        RegisterPage("settings", new SettingsViewModel(services), new SettingsView());

        SelectedNavTag = services.Settings.Current.LastPageTag ?? "dashboard";

        services.SelectedSubscriptionChanged += () =>
        {
            var id = services.SelectedSubscriptionId;
            SelectedSubscription = Subscriptions.FirstOrDefault(s => s.Id == id);
        };

        if (_pages.TryGetValue("settings", out var settingsPage)
            && settingsPage.Vm is SettingsViewModel settingsVm)
        {
            settingsVm.SignOutRequested += () => SignOut();
        }
    }

    public AppServices Services { get; }

    public List<NavItem> NavItems { get; }

    [ObservableProperty]
    private bool _isSignedIn;

    [ObservableProperty]
    private UserProfile? _account;

    [ObservableProperty]
    private Control? _currentView;

    [ObservableProperty]
    private string _selectedNavTag = "dashboard";

    [ObservableProperty]
    private IReadOnlyList<AzureSubscription> _subscriptions = System.Array.Empty<AzureSubscription>();

    [ObservableProperty]
    private AzureSubscription? _selectedSubscription;

    [ObservableProperty]
    private bool _isPaneOpen = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _themeLabel = "跟随系统";

    partial void OnSelectedNavTagChanged(string value)
    {
        if (_pages.TryGetValue(value, out var page))
        {
            CurrentView = page.View;
            _ = page.Vm.LoadAsync();
        }

        Services.Settings.Update(s => s.LastPageTag = value);
    }

    partial void OnSelectedSubscriptionChanged(AzureSubscription? value)
    {
        Services.SelectedSubscriptionId = value?.Id;
        Services.Settings.Update(s => s.LastSubscriptionId = value?.Id);
        if (IsSignedIn)
        {
            _ = ReloadCurrentPageAsync();
        }
    }

    partial void OnIsPaneOpenChanged(bool value)
    {
        Services.Settings.Update(s => s.IsPaneOpen = value);
    }

    private void RegisterPage(string tag, PageViewModel vm, Control view)
    {
        view.DataContext = vm;
        _pages[tag] = (vm, view);
    }

    // ----------------------------------------------------------- 生命周期

    public async Task InitializeAsync()
    {
        IsPaneOpen = Services.Settings.Current.IsPaneOpen;
        SyncThemeLabel();

        // 仅供无头渲染自检：直接以已登录态展示主壳，不走真实认证恢复。
        if (PretendSignedInForRender)
        {
            IsSignedIn = true;
            return;
        }

        try
        {
            if (await Services.Auth.TryRestoreAsync())
            {
                await CompleteLoginAsync();
                return;
            }
        }
        catch
        {
            // 恢复失败就回到登录页，不阻塞启动。
        }

        IsSignedIn = false;
    }

    /// <summary>仅供无头渲染自检使用，正式运行恒为 false。</summary>
    public bool PretendSignedInForRender { get; set; }

    public async Task CompleteLoginAsync()
    {
        Account = Services.Auth.CurrentUser;
        IsSignedIn = true;

        try
        {
            var subs = await Services.Subscriptions.ListAsync(ct: CancellationToken.None);
            Subscriptions = subs;
            var preferred = Services.Settings.Current.LastSubscriptionId;
            SelectedSubscription = subs.FirstOrDefault(s => s.Id == preferred)
                                  ?? subs.FirstOrDefault(s => s.IsEnabled)
                                  ?? subs.FirstOrDefault();
        }
        catch (System.Exception ex)
        {
            // 订阅列表拉取失败也先进入主界面，页面会显示错误。
            Services.SelectedSubscriptionId = null;
        }

        // 跳到上次停留的页面或概览。
        var tag = Services.Settings.Current.LastPageTag ?? "dashboard";
        SelectedNavTag = _pages.ContainsKey(tag) ? tag : "dashboard";
    }

    [RelayCommand]
    private void SignOut()
    {
        Services.Auth.SignOut();
        Account = null;
        IsSignedIn = false;
    }

    [RelayCommand]
    private void Navigate(string tag)
    {
        if (_pages.ContainsKey(tag))
        {
            SelectedNavTag = tag;
        }
    }

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;

    [RelayCommand]
    private void Search()
    {
        var text = SearchText.Trim();
        _resourcesVm.FilterText = text;
        Navigate("resources");
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var current = Services.Settings.Current.Theme;
        var next = current switch
        {
            "Light" => "Dark",
            "Dark" => "Default",
            _ => "Light",
        };
        Services.Settings.Update(s => s.Theme = next);
        SyncThemeLabel();
    }

    private void SyncThemeLabel()
    {
        ThemeLabel = Services.Settings.Current.Theme switch
        {
            "Light" => "浅色",
            "Dark" => "深色",
            _ => "跟随系统",
        };
    }

    private async Task ReloadCurrentPageAsync()
    {
        if (_pages.TryGetValue(SelectedNavTag, out var page))
        {
            await page.Vm.LoadAsync();
        }
    }
}
