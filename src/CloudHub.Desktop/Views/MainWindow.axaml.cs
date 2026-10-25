using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CloudHub.Desktop.ViewModels;

namespace CloudHub.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.InitializeAsync();
        }

        if (this.FindControl<LoginView>("LoginControl") is { } login)
        {
            login.LoginSucceeded -= OnLoginSucceeded;
            login.LoginSucceeded += OnLoginSucceeded;
        }
    }

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.CompleteLoginAsync();
        }
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.SearchCommand.Execute(null);
        }
    }
}
