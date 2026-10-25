using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CloudHub.Core.Models;
using CloudHub.Core.Services;

namespace CloudHub.Desktop.Converters;

/// <summary>bool → true/false（Avalonia 的 IsVisible 是 bool）。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;
}

/// <summary>bool 取反 → true/false（Avalonia 的 IsVisible 是 bool）。</summary>
public sealed class InvertedBoolToVisibilityConverter : IValueConverter
{
    public static readonly InvertedBoolToVisibilityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>bool 取反。</summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    public static readonly InvertedBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is false;
}

/// <summary>对象非空 → true（用于「有值才显示」）。</summary>
public sealed class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

/// <summary>字符串非空 → true。</summary>
public sealed class StringNotEmptyConverter : IValueConverter
{
    public static readonly StringNotEmptyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

/// <summary>把 VM 电源状态 / 资源状态映射成一枚语义色（绿/红/琥珀/灰）。</summary>
public sealed class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        var key = parameter as string ?? "auto";

        if (key == "power")
        {
            return text.ToLowerInvariant() switch
            {
                "running" => new SolidColorBrush(Color.Parse("#107C10")),
                "deallocated" => new SolidColorBrush(Color.Parse("#C42B1C")),
                "stopped" => new SolidColorBrush(Color.Parse("#C42B1C")),
                "starting" or "stopping" or "restarting" or "deallocating" => new SolidColorBrush(Color.Parse("#B25000")),
                _ => new SolidColorBrush(Color.Parse("#888888")),
            };
        }

        if (key == "status")
        {
            return text.ToLowerInvariant() switch
            {
                "failed" or "error" => new SolidColorBrush(Color.Parse("#C42B1C")),
                "warning" => new SolidColorBrush(Color.Parse("#B25000")),
                "succeeded" or "success" or "running" => new SolidColorBrush(Color.Parse("#107C10")),
                _ => new SolidColorBrush(Color.Parse("#888888")),
            };
        }

        // 通用：非「成功/运行」即视为需关注
        return text.ToLowerInvariant() switch
        {
            "running" or "succeeded" or "success" or "enabled" or "active" => new SolidColorBrush(Color.Parse("#107C10")),
            "failed" or "error" or "deallocated" or "stopped" or "disabled" => new SolidColorBrush(Color.Parse("#C42B1C")),
            "warning" or "starting" or "stopping" or "pending" => new SolidColorBrush(Color.Parse("#B25000")),
            _ => new SolidColorBrush(Color.Parse("#888888")),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

/// <summary>十进制金额 → 带千分位的字符串。</summary>
public sealed class DecimalConverter : IValueConverter
{
    public static readonly DecimalConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            var digits = parameter as string == "2" ? 2 : 2;
            return d.ToString($"N{digits}", CultureInfo.CurrentCulture);
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

/// <summary>把 ARM 资源类型（microsoft.compute/virtualmachines）转成中文名（虚拟机）。</summary>
public sealed class ArmResourceTypeNameConverter : IValueConverter
{
    public static readonly ArmResourceTypeNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ArmResource r => ResourceTypeCatalog.DisplayName(r),
            string s => ResourceTypeCatalog.Describe(s).Display,
            _ => value?.ToString() ?? string.Empty,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

/// <summary>把资源分类枚举转成中文标签。</summary>
public sealed class CategoryNameConverter : IValueConverter
{
    public static readonly CategoryNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ResourceCategory c => ResourceTypeCatalog.CategoryDisplayName(c),
            _ => value?.ToString() ?? string.Empty,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}
