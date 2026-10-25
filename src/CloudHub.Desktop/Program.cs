using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using CloudHub.Desktop.Services;
using CloudHub.Desktop.ViewModels;
using CloudHub.Desktop.Views;

namespace CloudHub.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var builder = AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace();

        // 无显示环境（CI / macOS 无 GUI 会话）下跑自检，不需要真实窗口。
        if (Environment.GetEnvironmentVariable("CLOUDHUB_HEADLESS") == "1")
        {
            builder.UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
            builder.SetupWithoutStarting();

            var settings = new SettingsService();
            _ = AppServices.Create(settings);
            App.RunSelfTest();
            return 0;
        }

        // 无显示环境下把关键界面渲染成 PNG，验证真实视觉与样式链路。
        if (Environment.GetEnvironmentVariable("CLOUDHUB_RENDER") is { Length: > 0 } renderDir)
        {
            builder.UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
            builder.SetupWithoutStarting();

            var settings = new SettingsService();
            var services = AppServices.Create(settings);

            var themeVar = Environment.GetEnvironmentVariable("CLOUDHUB_RENDER_THEME") == "dark"
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
            if (App.Current is not null) App.Current.RequestedThemeVariant = themeVar;
            _renderTheme = themeVar;

            RenderToPng(System.IO.Path.Combine(renderDir, "login.png"),
                new LoginView(), new Size(900, 560));

            RenderToPng(System.IO.Path.Combine(renderDir, "dashboard.png"),
                new DashboardView { DataContext = new DashboardViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "resources.png"),
                new ResourcesView { DataContext = new ResourcesViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "resourcegroups.png"),
                new ResourceGroupsView { DataContext = new ResourceGroupsViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "vms.png"),
                new VirtualMachinesView { DataContext = new VirtualMachinesViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "cost.png"),
                new CostView { DataContext = new CostViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "activitylog.png"),
                new ActivityLogView { DataContext = new ActivityLogViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "subscriptions.png"),
                new SubscriptionsView { DataContext = new SubscriptionsViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "allservices.png"),
                new AllServicesView { DataContext = new AllServicesViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "resourcegraph.png"),
                new ResourceGraphView { DataContext = new ResourceGraphViewModel(services) }, new Size(1180, 760));
            RenderToPng(System.IO.Path.Combine(renderDir, "settings.png"),
                new SettingsView { DataContext = new SettingsViewModel(services) }, new Size(1180, 760));

            // 主壳（强制已登录态，展示导航 + 内容布局）
            var shellVm = new MainWindowViewModel(services)
            {
                IsSignedIn = true,
                PretendSignedInForRender = true,
                CurrentView = new DashboardView { DataContext = new DashboardViewModel(services) }
            };
            RenderToPng(System.IO.Path.Combine(renderDir, "shell.png"),
                new MainWindow { DataContext = shellVm }, new Size(1180, 760));

            Console.WriteLine("CLOUDHUB_RENDER=OK");
            return 0;
        }

        builder.UsePlatformDetect();
        return builder.StartWithClassicDesktopLifetime(args);
    }

    private static ThemeVariant _renderTheme = ThemeVariant.Light;

    private static void RenderToPng(string path, Control control, Size size)
    {
        // MainWindow 本身是 Window，不能再包进另一个 Window 的 Content。
        var window = control as Window ?? new Window { Content = control };
        window.RequestedThemeVariant = _renderTheme;
        window.Width = size.Width;
        window.Height = size.Height;
        window.Show();

        // 关键：UseSkia() + UseHeadlessDrawing=false 才有真实像素；
        // 渲染由渲染计时器延迟触发，CaptureRenderedFrame 内部会强制 tick 并捕获最后一帧。
        var frame = HeadlessWindowExtensions.CaptureRenderedFrame(window);

        if (frame is null)
        {
            Console.WriteLine($"RENDER_FAIL {path}");
            return;
        }

        frame.Save(path);
        Console.WriteLine($"RENDERED {path} size={frame.Size}");
    }
}
