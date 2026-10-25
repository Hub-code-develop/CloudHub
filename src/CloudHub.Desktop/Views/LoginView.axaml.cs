using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using CloudHub.Core.Auth;
using CloudHub.Desktop.Services;

namespace CloudHub.Desktop.Views;

public partial class LoginView : UserControl
{
    private CancellationTokenSource? _cts;

    public LoginView()
    {
        InitializeComponent();

        // LoginView 在 MainWindow 里是单例、随登录态反复显隐。
        // 每次重新可见（例如退出登录后回到登录页）都必须把上一次残留的
        // 设备码 / 轮询状态彻底清掉，否则会显示上一轮的旧验证码、旧链接，
        // 且「取消」只能 cancel 一个早已跑完的 CTS，看起来像「无法取消 / 复用同一套码」。
        ResetToInitialState();
        PropertyChanged += OnVisibilityChanged;
    }

    private void OnVisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && e.NewValue is true)
        {
            ResetToInitialState();
        }
    }

    /// <summary>登录成功后由 MainWindow 订阅，用于切换到主界面。</summary>
    public event EventHandler? LoginSucceeded;

    /// <summary>
    /// 回到干净的初始登录态：取消并丢弃任何残留的轮询任务，
    /// 隐藏设备码面板、显示登录按钮、清空旧验证码 / 旧链接。
    /// </summary>
    private void ResetToInitialState(bool includeError = true)
    {
        try { _cts?.Cancel(); } catch { /* 已完成的 CTS cancel 不会抛，忽略 */ }
        _cts?.Dispose();
        _cts = null;
        _openUrl = null;

        LoginButton.IsVisible = true;
        CodePanel.IsVisible = false;
        WaitBar.IsVisible = false;
        CodeText.Text = "— — — —";
        StatusText.Text = string.Empty;
        if (includeError) ErrorText.Text = string.Empty;
    }

    private async void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        await BeginDeviceCodeFlowAsync();
    }

    private async Task BeginDeviceCodeFlowAsync()
    {
        LoginButton.IsVisible = false;
        CodePanel.IsVisible = true;
        ErrorText.Text = string.Empty;
        StatusText.Text = "正在获取设备代码…";
        WaitBar.IsVisible = true;

        // 上一轮若残留了 CTS（理论上不会，但保险），先清掉再开新的。
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var auth = AppServices.Current.Auth;
            var challenge = await auth.BeginDeviceCodeFlowAsync(_cts.Token);

            CodeText.Text = challenge.UserCode;
            _openUrl = challenge.VerificationUriComplete ?? challenge.VerificationUri;

            StatusText.Text = $"请打开 {_openUrl} 并输入代码。";

            var tokens = await auth.PollForTokenAsync(challenge, _cts.Token);

            // 成功：丢弃轮询用的 CTS（整个 LoginView 即将被主壳隐藏）。
            // 状态复位留给「下次重新可见」时统一处理，避免这里直接隐藏 CodePanel
            // 又漏掉某些字段导致残留。
            _cts?.Dispose();
            _cts = null;

            WaitBar.IsVisible = false;
            StatusText.Text = "登录成功，正在加载…";
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            // 用户主动取消：回到初始登录态，等待下一次发起新流程（必定获取全新设备码）。
            ResetToInitialState();
        }
        catch (CloudHubAuthException ex)
        {
            ResetToInitialState(includeError: false);
            ErrorText.Text = FriendlyLoginError(ex);
        }
        catch (Exception ex)
        {
            ResetToInitialState(includeError: false);
            ErrorText.Text = "登录失败：" + ex.Message;
        }
    }

    private string? _openUrl;

    private void CopyCode_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(CodeText.Text) && CodeText.Text != "— — — —")
        {
            TopLevel? top = TopLevel.GetTopLevel(this);
            top?.Clipboard?.SetTextAsync(CodeText.Text);
            StatusText.Text = "代码已复制。";
        }
    }

    private void OpenBrowser_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_openUrl))
        {
            return;
        }

        // 仅在真实运行环境打开浏览器，自检时不触发。
        if (Environment.GetEnvironmentVariable("CLOUDHUB_SELFTEST") == "1")
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _openUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
            StatusText.Text = "无法自动打开浏览器，请手动访问：" + _openUrl;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        // 取消：中断轮询并复位到初始登录态。
        // BeginDeviceCodeFlowAsync 里的轮询会在下一个 interval 处因取消而抛出，
        // 由 OperationCanceledException 分支统一复位；这里也兜底 cancel 一次。
        _cts?.Cancel();
    }

    /// <summary>
    /// 把登录错误翻译成人话。重点识别「当前 app 的 client id 只支持组织账号、拒绝个人
    /// Microsoft 账号」这一类——CloudHub 默认借用 Azure CLI 的 client id，而它就是 org-only，
    /// 个人账号会被 Azure 直接拒绝，错误信息里会带 personal / work or school / organizational 等字眼。
    /// </summary>
    private static readonly string[] PersonalAccountBlockKeywords =
    {
        "personal microsoft account", "work or school account", "organizational account",
        "organisational account", "aadsts50020", "aadsts70001", "aadsts7000112",
        "this kind of application", "cannot be used", "cannot sign in",
        "only supports", "microsoft account can",
    };

    private static readonly string[] AdminConsentBlockKeywords =
    {
        "aadsts1000031", "cannot be accessed at this time", "contact your administrator",
        "admin consent", "requires admin", "aadsts65001",
    };

    private static readonly string[] PublicClientBlockKeywords =
    {
        "aadsts7000218", "client_assertion", "client_secret",
    };

    private static string FriendlyLoginError(CloudHubAuthException ex)
    {
        var hay = (ex.Description ?? ex.Message ?? string.Empty).ToLowerInvariant();
        var isPersonalBlock = PersonalAccountBlockKeywords.Any(k => hay.Contains(k));
        var isAdminConsentBlock = AdminConsentBlockKeywords.Any(k => hay.Contains(k));
        var isPublicClientBlock = PublicClientBlockKeywords.Any(k => hay.Contains(k));

        if (isPublicClientBlock)
        {
            return "登录被 Entra 拦截（AADSTS7000218）：CloudHub 走的是「公共客户端设备代码流」" +
                   "（不发客户端密码，和 Azure CLI 一样），但当前 app 没开启公共客户端支持，\n" +
                   "Entra 因此要求提供 client_secret / client_assertion。\n" +
                   "请在 Entra 里开启（无需改代码）：\n" +
                   "entra.microsoft.com → 应用注册 → CloudHub → 身份验证 → 高级设置 → " +
                   "「允许公共客户端流」设为【是】。\n" +
                   "（等价做法：为该应用新建客户端密码并在 CloudHub 设置填 ClientSecret，但桌面 app 不建议存密码。）\n" +
                   "原始错误：" + (ex.Description ?? ex.Message);
        }

        if (isAdminConsentBlock)
        {
            return "登录被 Entra 拦截（AADSTS1000031）：CloudHub 请求了「Azure Service Management」的 " +
                   "user_impersonation 权限，该权限要求【管理员同意】。\n" +
                   "个人 Microsoft 账号（outlook.com 等）无法自行授予管理员同意，所以被挡下。\n" +
                   "解决方法（二选一）：\n" +
                   "① 改用你的【工作/租户管理员账号】登录 CloudHub（它是 Hub-dev 的成员/管理员），\n" +
                   "   然后到 entra.microsoft.com → 应用注册 → CloudHub → API 权限 → " +
                   "「为 Hub-dev 授予管理员同意」。\n" +
                   "② 或把 CloudHub 这个 app 改成【单租户】(仅 Hub-dev)，再由 Hub-dev 管理员授予同意。\n" +
                   "原始错误：" + (ex.Description ?? ex.Message);
        }

        if (isPersonalBlock)
        {
            return "登录被拒：当前 ClientId 只支持「组织/工作账号」，不接受个人 Microsoft 账号。\n" +
                   "要用个人账号登录，请到 Entra 注册一个支持个人账号的 app：\n" +
                   "1) entra.microsoft.com → 应用注册 → 新建，账户类型选「任何组织目录中的账户和个人 Microsoft 账户」\n" +
                   "2) 复制「应用程序(客户端) ID」\n" +
                   "3) API 权限 → 添加「Azure Service Management」的 user_impersonation（委托权限）\n" +
                   "4) CloudHub 设置 → ClientId 填你的 ID，TenantId 保持 common（默认）\n" +
                   "原始错误：" + (ex.Description ?? ex.Message);
        }

        return "登录失败：" + (ex.Description ?? ex.Message);
    }
}
