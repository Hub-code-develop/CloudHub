using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CloudHub.Core.Services;
using CloudHub.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;

namespace CloudHub.Desktop.ViewModels;

/// <summary>
/// 「所有服务」页：把 Azure 门户「所有服务」里的全部 Azure 服务按分类列出，
/// 支持搜索与按类别筛选，点击任一项在默认浏览器打开对应的 Azure 门户页。
/// </summary>
public partial class AllServicesViewModel : PageViewModel
{
    private readonly IReadOnlyList<AzureServiceItem> _all = AzureServiceCatalog.AllServices;

    public AllServicesViewModel(AppServices services) : base(services)
    {
        Rebuild();
    }

    public override string Title => "所有服务";

    public override FASymbol Icon => FASymbol.AllApps;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "全部";

    /// <summary>类别筛选下拉：全部 + 17 个分类显示名。</summary>
    public IReadOnlyList<string> Categories { get; } =
        new[] { "全部" }.Concat(AzureServiceCatalog.Categories.Select(c => c.DisplayName)).ToList();

    [ObservableProperty]
    private IReadOnlyList<ServiceGroup> _groups = System.Array.Empty<ServiceGroup>();

    /// <summary>按选中类别 + 搜索词过滤后，分组展示的服务集合。</summary>
    private void Rebuild()
    {
        var q = _all.AsEnumerable();

        if (SelectedCategory != "全部")
        {
            var cat = AzureServiceCatalog.Categories.FirstOrDefault(c => c.DisplayName == SelectedCategory);
            if (cat is { } c)
            {
                q = q.Where(i => i.Category == c.Key);
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var f = SearchText.Trim();
            q = q.Where(i => i.Name.Contains(f, System.StringComparison.OrdinalIgnoreCase));
        }

        var grouped = q
            .GroupBy(i => i.Category)
            .Select(g => new ServiceGroup(
                AzureServiceCatalog.Categories.First(c => c.Key == g.Key).DisplayName,
                g.OrderBy(i => i.Name, System.StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(g => g.CategoryName, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        Groups = grouped;
        EmptyHint = grouped.Count == 0 ? "没有匹配的服务。" : null;
    }

    partial void OnSearchTextChanged(string value) => Rebuild();

    partial void OnSelectedCategoryChanged(string value) => Rebuild();

    [RelayCommand]
    private void OpenInPortal(AzureServiceItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = item.PortalUrl, UseShellExecute = true });
        }
        catch
        {
            // 浏览器唤起失败（例如无默认浏览器）时静默忽略，不阻断 UI。
        }
    }
}

/// <summary>按类别分组后的展示模型：类别名 + 该类别下的服务项。</summary>
public sealed record ServiceGroup(string CategoryName, IReadOnlyList<AzureServiceItem> Items);
