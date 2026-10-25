using CloudHub.Core.Arm;
using CloudHub.Core.Models;

namespace CloudHub.Core.Services;

public enum VmPowerAction
{
    Start,
    Restart,
    Stop,
    Deallocate,
}

/// <summary>虚拟机：列举（带电源状态）+ 真实的启停 / 重启 / 解除分配。</summary>
public sealed class VirtualMachineService(ArmClient arm)
{
    private const string ApiVersion = "2024-07-01";

    private readonly ArmClient _arm = arm;

    /// <summary>
    /// 列出订阅内所有 VM。<c>statusOnly=true</c> 会让 ARM 在列表里就带上 instanceView，
    /// 省掉每个 VM 一次详情请求（门户就是这么干的）。
    /// </summary>
    public async Task<IReadOnlyList<VirtualMachine>> ListAsync(string subscriptionId, CancellationToken ct = default)
    {
        var items = await _arm.GetAllPagedAsync(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.Compute/virtualMachines" +
            $"?api-version={ApiVersion}&statusOnly=true", ct).ConfigureAwait(false);

        return items
            .Select(VirtualMachine.FromJson)
            .Where(vm => !string.IsNullOrEmpty(vm.Id))
            .Select(vm => vm with
            {
                VcpuCount = VirtualMachine.EstimateSku(vm.VmSize).Vcpu,
                MemoryMb = VirtualMachine.EstimateSku(vm.VmSize).MemoryMb,
            })
            .OrderBy(vm => vm.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>刷新单台 VM 的电源状态。</summary>
    public async Task<VmPowerState> GetPowerStateAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken ct = default)
    {
        var response = await _arm.GetAsync(
            $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/" +
            $"{vmName}/instanceView?api-version={ApiVersion}", ct).ConfigureAwait(false);

        var statuses = response.Body is { } body
            ? Models.JsonHelp.Arr(body, "statuses")
            : [];

        return VmPowerState.FromStatuses(statuses);
    }

    /// <summary>下发电源操作并等到它真正完成。</summary>
    public async Task ApplyPowerActionAsync(
        string subscriptionId,
        string resourceGroup,
        string vmName,
        VmPowerAction action,
        CancellationToken ct = default)
    {
        var verb = action switch
        {
            VmPowerAction.Start => "start",
            VmPowerAction.Restart => "restart",
            VmPowerAction.Stop => "powerOff",
            VmPowerAction.Deallocate => "deallocate",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        var url = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}" +
                  $"/providers/Microsoft.Compute/virtualMachines/{vmName}/{verb}?api-version={ApiVersion}";

        var response = await _arm.PostAsync(url, null, ct).ConfigureAwait(false);
        await _arm.WaitForOperationAsync(response, ct).ConfigureAwait(false);
    }

    /// <summary>删除一台 VM（连同其 OS 盘需要单独处理，这里只删 VM 本体）。</summary>
    public async Task DeleteAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken ct = default)
    {
        var response = await _arm.DeleteAsync(
            $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/" +
            $"{vmName}?api-version={ApiVersion}", ct).ConfigureAwait(false);

        await _arm.WaitForOperationAsync(response, ct).ConfigureAwait(false);
    }
}
