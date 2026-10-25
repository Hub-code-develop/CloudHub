using CloudHub.Core.Arm;
using CloudHub.Core.Auth;
using CloudHub.Core.Services;

namespace CloudHub.Desktop.Services;

/// <summary>
/// 极简服务容器 —— 不引 DI 框架，够用就行。
/// 设置变更时会整体重建 ARM 客户端与认证服务（端点 / client id 换了旧连接就不该复用）。
/// </summary>
public sealed class AppServices : IDisposable
{
    private AppServices(SettingsService settings, AuthService auth, ArmClient arm)
    {
        Settings = settings;
        Auth = auth;
        Arm = arm;
        Subscriptions = new SubscriptionService(arm, auth);
        ResourceGroups = new ResourceGroupService(arm);
        Resources = new ResourceService(arm);
        VirtualMachines = new VirtualMachineService(arm);
        Costs = new CostService(arm);
        ActivityLog = new ActivityLogService(arm);
        ResourceGraph = new ResourceGraphService(arm);
    }

    public static AppServices Current { get; private set; } = null!;

    public SettingsService Settings { get; }

    public AuthService Auth { get; }

    public ArmClient Arm { get; }

    public SubscriptionService Subscriptions { get; }

    public ResourceGroupService ResourceGroups { get; }

    public ResourceService Resources { get; }

    public VirtualMachineService VirtualMachines { get; }

    public CostService Costs { get; }

    public ActivityLogService ActivityLog { get; }

    public ResourceGraphService ResourceGraph { get; }

    /// <summary>当前选中的订阅（跨页面共享）。</summary>
    private string? _selectedSubscriptionId;

    public string? SelectedSubscriptionId
    {
        get => _selectedSubscriptionId;
        set
        {
            if (_selectedSubscriptionId == value)
            {
                return;
            }

            _selectedSubscriptionId = value;
            SelectedSubscriptionChanged?.Invoke();
        }
    }

    public event System.Action? SelectedSubscriptionChanged;

    public static AppServices Create(SettingsService settings)
    {
        var auth = new AuthService(settings.Current.ToAuthOptions());
        var arm = new ArmClient(auth.GetAccessTokenAsync, settings.Current.EffectiveArmEndpoint);
        var services = new AppServices(settings, auth, arm);
        Current = services;
        return services;
    }

    public void Dispose()
    {
        Auth.Dispose();
        Arm.Dispose();
    }
}
