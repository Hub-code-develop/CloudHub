using System.Collections.Generic;
using CloudHub.Desktop.Services;
using CommunityToolkit.Mvvm.Input;

namespace CloudHub.Desktop.ViewModels;

public sealed partial class SettingsViewModel : PageViewModel
{
    public SettingsViewModel(AppServices services) : base(services)
    {
        Settings = services.Settings.Current;
        CloudOptions = CloudPresets.All;
    }

    public override string Title => "设置";

    public AppSettings Settings { get; }

    public IReadOnlyList<CloudPreset> CloudOptions { get; }

    public event System.Action? SignOutRequested;

    public IReadOnlyList<string> Themes { get; } = new[] { "Default", "Light", "Dark" };

    [RelayCommand]
    private void ApplyTheme(string theme)
    {
        Services.Settings.Update(s => s.Theme = theme);
    }

    [RelayCommand]
    private void ApplyCloud(string cloudId)
    {
        Services.Settings.Update(s => s.CloudId = cloudId);
    }

    [RelayCommand]
    private void SaveIdentity()
    {
        Services.Settings.Save();
    }

    [RelayCommand]
    private void SignOut() => SignOutRequested?.Invoke();
}
