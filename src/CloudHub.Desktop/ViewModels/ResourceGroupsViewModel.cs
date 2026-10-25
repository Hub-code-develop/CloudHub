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

public sealed record ResourceGroupRow(ResourceGroup Group, int ResourceCount);

public sealed partial class ResourceGroupsViewModel : PageViewModel
{
    public ResourceGroupsViewModel(AppServices services) : base(services)
    {
    }

    public override string Title => "资源组";

    [ObservableProperty] private IReadOnlyList<ResourceGroupRow> _items = System.Array.Empty<ResourceGroupRow>();

    [ObservableProperty] private ResourceGroupRow? _selected;

    [ObservableProperty] private string _newName = string.Empty;

    [ObservableProperty] private string _newLocation = string.Empty;

    [ObservableProperty] private IReadOnlyList<AzureLocation> _locations = System.Array.Empty<AzureLocation>();

    [ObservableProperty] private bool _isCreating;

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
            var groupsTask = Services.ResourceGroups.ListAsync(subId, ct);
            var resourcesTask = Services.Resources.ListAsync(subId, ct: ct);
            var locationsTask = Services.Subscriptions.ListLocationsAsync(subId, ct);

            await Task.WhenAll(groupsTask, resourcesTask, locationsTask);

            var groups = await groupsTask;
            var resources = await resourcesTask;
            Locations = await locationsTask;
            NewLocation = Locations.Count > 0 ? Locations[0].Name : "eastasia";

            var counts = resources
                .GroupBy(r => r.ResourceGroupName, System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), System.StringComparer.OrdinalIgnoreCase);

            Items = groups
                .Select(g => new ResourceGroupRow(g, counts.TryGetValue(g.Name, out var c) ? c : 0))
                .ToList();

            EmptyHint = Items.Count == 0 ? "还没有任何资源组，点「新建资源组」创建一个。" : null;
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
    private async Task CreateAsync()
    {
        var subId = Services.SelectedSubscriptionId;
        if (string.IsNullOrWhiteSpace(subId) || string.IsNullOrWhiteSpace(NewName))
        {
            Error = "请填写资源组名称。";
            return;
        }

        IsCreating = true;
        Error = null;
        try
        {
            await Services.ResourceGroups.CreateOrUpdateAsync(subId!, NewName.Trim(), NewLocation);
            NewName = string.Empty;
            await LoadAsync();
        }
        catch (System.Exception ex)
        {
            Error = "创建失败：" + Summarize(ex);
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(ResourceGroupRow? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            await Services.ResourceGroups.DeleteAsync(row.Group.SubscriptionId, row.Group.Name, CancellationToken.None);
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
