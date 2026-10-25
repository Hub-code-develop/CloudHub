using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using CloudHub.Core.Models;
using CloudHub.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CloudHub.Desktop.Services;

namespace CloudHub.Desktop.ViewModels;

public partial class ResourcesViewModel : PageViewModel
{
    private IReadOnlyList<ArmResource> _all = System.Array.Empty<ArmResource>();
    private readonly ResourceCategory[]? _fixedCategories;

    public ResourcesViewModel(AppServices services, params ResourceCategory[] fixedCategories) : base(services)
    {
        _fixedCategories = fixedCategories.Length == 0 ? null : fixedCategories;

        // 分类筛选下拉：固定分类顺序 + 顶部「全部」。服务族页预置分类后隐藏这个下拉。
        Categories = new[] { "全部" }.Concat(
            ResourceTypeCatalog.Categories.Select(ResourceTypeCatalog.CategoryDisplayName)).ToList();

        if (_fixedCategories is not null)
        {
            IsCategoryPickerVisible = false;
        }
    }

    public override string Title => _fixedCategories is { Length: > 0 } cats
        ? string.Join(" / ", cats.Select(ResourceTypeCatalog.CategoryDisplayName))
        : "资源";

    [ObservableProperty] private IReadOnlyList<ArmResource> _items = System.Array.Empty<ArmResource>();

    [ObservableProperty] private string _filterText = string.Empty;

    [ObservableProperty] private string _selectedCategory = "全部";

    [ObservableProperty] private IReadOnlyList<string> _categories = new[] { "全部" };

    [ObservableProperty] private bool _isCategoryPickerVisible = true;

    [ObservableProperty] private string _typeFilter = "全部类型";

    [ObservableProperty] private IReadOnlyList<string> _typeFilters = new[] { "全部类型" };

    [ObservableProperty] private ArmResource? _selected;

    public string DetailJson => Selected is null ? string.Empty : Pretty(Selected.Raw);

    [ObservableProperty] private bool _showDetail;

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    partial void OnTypeFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedChanged(ArmResource? value)
    {
        ShowDetail = value is not null;
        OnPropertyChanged(nameof(DetailJson));
    }

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
            _all = await Services.Resources.ListAsync(subId, ct: ct);
            TypeFilters = new[] { "全部类型" }.Concat(
                _all.Select(r => r.Provider).Distinct().OrderBy(p => p)).ToList();
            ApplyFilter();
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

    private void ApplyFilter()
    {
        var q = _all.AsEnumerable();

        // 分类筛选：服务族页用预置分类集合，通用页用下拉选择。
        if (_fixedCategories is { Length: > 0 } cats)
        {
            q = q.Where(r => cats.Contains(ResourceTypeCatalog.CategoryOf(r)));
        }
        else if (SelectedCategory != "全部")
        {
            var cat = ResourceTypeCatalog.Categories.FirstOrDefault(
                c => ResourceTypeCatalog.CategoryDisplayName(c) == SelectedCategory);
            if (cat is { } c)
            {
                q = q.Where(r => ResourceTypeCatalog.CategoryOf(r) == c);
            }
        }

        if (!string.IsNullOrWhiteSpace(TypeFilter) && TypeFilter != "全部类型")
        {
            q = q.Where(r => r.Provider == TypeFilter);
        }

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var f = FilterText.Trim();
            q = q.Where(r => r.Name.Contains(f, System.StringComparison.OrdinalIgnoreCase)
                             || r.Type.Contains(f, System.StringComparison.OrdinalIgnoreCase)
                             || r.ResourceGroupName.Contains(f, System.StringComparison.OrdinalIgnoreCase));
        }

        Items = q.ToList();
        if (Items.Count == 0)
        {
            EmptyHint = _all.Count == 0 ? "这个订阅下还没有资源。" : "没有匹配的资源。";
        }
        else
        {
            EmptyHint = null;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(ArmResource? resource)
    {
        if (resource is null)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            await Services.Resources.DeleteAsync(resource.Id, ct: CancellationToken.None);
            _all = _all.Where(r => r.Id != resource.Id).ToList();
            Selected = null;
            ApplyFilter();
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

    private static string Pretty(System.Text.Json.JsonElement element)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(element,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return element.ToString();
        }
    }
}
