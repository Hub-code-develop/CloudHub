using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CloudHub.Core.Models;
using CloudHub.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CloudHub.Desktop.Services;

namespace CloudHub.Desktop.ViewModels;

public sealed partial class SubscriptionsViewModel : PageViewModel
{
    public SubscriptionsViewModel(AppServices services) : base(services)
    {
    }

    public override string Title => "订阅";

    [ObservableProperty] private IReadOnlyList<AzureSubscription> _items = System.Array.Empty<AzureSubscription>();

    public string? SelectedSubscriptionId => Services.SelectedSubscriptionId;

    // 当前登录的 Azure 账户（从缓存令牌解析），用于让用户一眼确认 CloudHub 到底登的是哪个账号。
    // 很多「订阅扫不到」其实是登错了账号（例如工作账号 vs 个人账号），这一行能直接暴露问题。
    public string? CurrentAccountUpn => Services.Auth.CurrentUser?.UserPrincipalName;
    public string? CurrentAccountName => Services.Auth.CurrentUser?.DisplayName;
    public string? CurrentAccountTenant => Services.Auth.CurrentUser?.TenantId;

    public string? CurrentAccountSummary =>
        CurrentAccountUpn is null ? null : $"{CurrentAccountName ?? CurrentAccountUpn} · {CurrentAccountUpn}";

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        Error = null;
        // 每次进入订阅页都刷新「当前登录账户」显示，避免退出重登后还显示旧账号。
        OnPropertyChanged(nameof(CurrentAccountUpn));
        OnPropertyChanged(nameof(CurrentAccountName));
        OnPropertyChanged(nameof(CurrentAccountTenant));
        OnPropertyChanged(nameof(CurrentAccountSummary));
        try
        {
            Items = await Services.Subscriptions.ListAsync(forceRefresh: true, ct: ct);
            OnPropertyChanged(nameof(SelectedSubscriptionId));
            EmptyHint = Items.Count == 0
                ? "没有可用的订阅。常见原因：\n" +
                  "① 你登录的是组织/租户「工作账号」，而订阅挂在你的个人 Microsoft 账号(MSA)名下——两者是不同目录，工作账号看不到个人订阅。请改用个人账号登录，或到 Azure 门户把订阅的「目录」切到该租户。\n" +
                  "② 即便你是租户管理员，Entra 目录管理员角色 ≠ 订阅的 RBAC 权限。需在订阅的「访问控制(IAM)」里把该账号加为 所有者/参与者/读取者。\n" +
                  "③ 「微软网站显示已拥有订阅」≠ 一定是 Azure 订阅：Microsoft 365 / Visual Studio / Game Pass 等是另一种订阅，不会出现在 Azure 门户；登录微软账号自动生成的只是免费 Azure AD 目录（不是订阅）。请以 portal.azure.com 的「订阅」页为准——它显示为空就说明该账号下确实没有 Azure 订阅。也可点门户右上角头像 →「切换目录」逐个目录检查是否藏在别的目录里；若订阅是用另一个微软账号开的，需用那个账号登录。\n" +
                  "④ 免费试用已开通却看不到：试用是 30 天 / $200 额度，到期后被 Azure 标记为「已禁用/已取消」，门户默认隐藏这类订阅。请到门户「订阅」页点「显示已取消的订阅」或齿轮里勾选显示已禁用项；CloudHub 走原始 ARM 接口，通常连被隐藏的禁用订阅也会列出来。若订阅确已禁用，要能新建资源需先「升级为即用即付」（需绑卡）。"
                : null;
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
    private void UseThis(AzureSubscription? sub)
    {
        if (sub is null)
        {
            return;
        }

        Services.SelectedSubscriptionId = sub.Id;
        Services.Settings.Update(s => s.LastSubscriptionId = sub.Id);
        OnPropertyChanged(nameof(SelectedSubscriptionId));
    }

    [RelayCommand]
    private void Refresh() => _ = LoadAsync();

    private static string Summarize(System.Exception ex)
        => ex is CloudHub.Core.Arm.ArmException ae ? ae.Message : ex.Message;
}
