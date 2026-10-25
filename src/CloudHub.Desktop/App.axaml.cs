using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CloudHub.Desktop.Services;
using CloudHub.Desktop.ViewModels;
using CloudHub.Desktop.Views;

namespace CloudHub.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = new SettingsService();
        var services = AppServices.Create(settings);

        ApplyTheme(settings.Current.Theme);
        settings.Changed += (_, s) => ApplyTheme(s.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel(services);
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;

            // 启动时先尝试用缓存令牌恢复会话，再决定显示登录页还是主界面。
            _ = viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string? theme)
    {
        if (Current is null)
        {
            return;
        }

        Current.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    /// <summary>
    /// 运行时自检：编译期只校验类型，资源键 / 绑定 / 模板错误要构造时才暴露。
    /// 这里把每个页面都 new 一遍，一次抓出全部 XAML 加载错误。
    /// </summary>
    internal static void RunSelfTest()
    {
        var results = new List<string>();
        var failed = false;

        var pageTypes = new (string Name, Func<PageViewModel> Factory)[]
        {
            ("Dashboard", () => new DashboardViewModel(AppServices.Current)),
            ("Resources", () => new ResourcesViewModel(AppServices.Current)),
            ("ResourceGroups", () => new ResourceGroupsViewModel(AppServices.Current)),
            ("VirtualMachines", () => new VirtualMachinesViewModel(AppServices.Current)),
            ("Cost", () => new CostViewModel(AppServices.Current)),
            ("ActivityLog", () => new ActivityLogViewModel(AppServices.Current)),
            ("Subscriptions", () => new SubscriptionsViewModel(AppServices.Current)),
            ("ResourceGraph", () => new ResourceGraphViewModel(AppServices.Current)),
            ("Settings", () => new SettingsViewModel(AppServices.Current)),
        };

        var viewTypes = new (string Name, Func<Control> Factory)[]
        {
            ("LoginView", () => new LoginView()),
            ("DashboardView", () => new DashboardView()),
            ("ResourcesView", () => new ResourcesView()),
            ("ResourceGroupsView", () => new ResourceGroupsView()),
            ("VirtualMachinesView", () => new VirtualMachinesView()),
            ("CostView", () => new CostView()),
            ("ActivityLogView", () => new ActivityLogView()),
            ("SubscriptionsView", () => new SubscriptionsView()),
            ("ResourceGraphView", () => new ResourceGraphView()),
            ("SettingsView", () => new SettingsView()),
        };

        try
        {
            foreach (var (name, factory) in pageTypes)
            {
                _ = factory();
                results.Add($"PAGE OK   {name}");
            }

            foreach (var (name, factory) in viewTypes)
            {
                _ = factory();
                results.Add($"VIEW OK   {name}");
            }

            var window = new MainWindow();
            window.Close();
            results.Add("WINDOW OK MainWindow");

            var login = new LoginView();
            results.Add($"LOGIN OK  {login.GetType().Name}");
        }
        catch (Exception ex)
        {
            failed = true;
            results.Add("SELFTEST FAILED");
            results.Add(ex.ToString());
        }

        foreach (var line in results)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine(failed ? "CLOUDHUB_SELFTEST=FAIL" : "CLOUDHUB_SELFTEST=PASS");
        Console.Out.Flush();
        Environment.Exit(failed ? 2 : 0);
    }
}
