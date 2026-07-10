using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MsixInstallerComposer.Models;
using MsixInstallerComposer.Services;
using MsixInstallerComposer.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace MsixInstallerComposer.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ApplicationThemeService _applicationThemeService = App.Services.GetRequiredService<ApplicationThemeService>();
    private readonly DialogService _dialogService = App.Services.GetRequiredService<DialogService>();
    private bool _isSynchronizingControls = true;

    public SettingsPageViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsPageViewModel>();

        InitializeComponent();
    }

    private void OnSettingsPageLoaded(object sender, RoutedEventArgs routedEventArguments)
    {
        _applicationThemeService.RegisterThemeTarget(this);

        _isSynchronizingControls = true;
        try { ViewModel.Load(); }
        finally { _isSynchronizingControls = false; }
    }

    private void OnSettingsPageUnloaded(object sender, RoutedEventArgs routedEventArguments) { }

    private async void OnLanguageComboBoxSelectionChanged(object sender, SelectionChangedEventArgs routedEventArguments)
    {
        if (_isSynchronizingControls) return;
        await ShowDialogIfNeededAsync(await StartWithControlSynchronization(() => ViewModel.ApplyLanguageSelectedIndexAsync(LanguageComboBox.SelectedIndex)));
    }

    private async void OnThemeComboBoxSelectionChanged(object sender, SelectionChangedEventArgs routedEventArguments)
    {
        if (_isSynchronizingControls) return;
        await ShowDialogIfNeededAsync(await StartWithControlSynchronization(() => ViewModel.ApplyThemeSelectedIndexAsync(ThemeComboBox.SelectedIndex)));
    }

    private async void OnAutomaticUpdateCheckToggleSwitchToggled(object sender, RoutedEventArgs routedEventArguments)
    {
        if (_isSynchronizingControls) return;
        await ShowDialogIfNeededAsync(await StartWithControlSynchronization(() => ViewModel.ApplyAutomaticUpdateCheckEnabledAsync(AutomaticUpdateCheckToggleSwitch.IsOn)));
    }

    private Task<SettingsPageDialogData> StartWithControlSynchronization(Func<Task<SettingsPageDialogData>> action)
    {
        _isSynchronizingControls = true;
        try { return action(); }
        finally { _isSynchronizingControls = false; }
    }

    private async Task ShowDialogIfNeededAsync(SettingsPageDialogData dialogData)
    {
        if (dialogData is null) return;
        await _dialogService.ShowDialogAsync(dialogData.Title, dialogData.Message, dialogData.PrimaryButtonText, dialogData.SecondaryButtonText);
    }
}