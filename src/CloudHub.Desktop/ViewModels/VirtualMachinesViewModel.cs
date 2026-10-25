using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudHub.Core.Models;
using CloudHub.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CloudHub.Desktop.Services;

namespace CloudHub.Desktop.ViewModels;

public sealed partial class VirtualMachinesViewModel : PageViewModel
{
    public VirtualMachinesViewModel(AppServices services) : base(services)
    {
    }

    public override string Title => "虚拟机";

    [ObservableProperty] private IReadOnlyList<VirtualMachine> _items = System.Array.Empty<VirtualMachine>();

    [ObservableProperty] private VirtualMachine? _selected;

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
            Items = await Services.VirtualMachines.ListAsync(subId, ct);
            EmptyHint = Items.Count == 0 ? "这个订阅下没有虚拟机。" : null;
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
    private async Task StartAsync(VirtualMachine? vm) => await PowerAsync(vm, VmPowerAction.Start);

    [RelayCommand]
    private async Task RestartAsync(VirtualMachine? vm) => await PowerAsync(vm, VmPowerAction.Restart);

    [RelayCommand]
    private async Task StopAsync(VirtualMachine? vm) => await PowerAsync(vm, VmPowerAction.Stop);

    [RelayCommand]
    private async Task DeallocateAsync(VirtualMachine? vm) => await PowerAsync(vm, VmPowerAction.Deallocate);

    private async Task PowerAsync(VirtualMachine? vm, VmPowerAction action)
    {
        if (vm is null)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            await Services.VirtualMachines.ApplyPowerActionAsync(
                vm.SubscriptionId, vm.ResourceGroupName, vm.Name, action, CancellationToken.None);
            await LoadAsync();
        }
        catch (System.Exception ex)
        {
            Error = $"操作失败（{action}）：" + Summarize(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(VirtualMachine? vm)
    {
        if (vm is null)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            await Services.VirtualMachines.DeleteAsync(vm.SubscriptionId, vm.ResourceGroupName, vm.Name, CancellationToken.None);
            await LoadAsync();
        }
        catch (System.Exception ex)
        {
            Error = "删除失败：" + Summarize(ex);
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
