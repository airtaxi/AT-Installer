using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MsixInstallerComposer.Models;
using MsixInstallerComposer.Services;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.System;

namespace MsixInstallerComposer.ViewModels;

public sealed partial class SettingsPageViewModel(ApplicationSettings applicationSettings, ApplicationSettingsService applicationSettingsService, ApplicationThemeService applicationThemeService, StoreUpdateService storeUpdateService, LocalizationService localizationService, DialogService dialogService) : ObservableObject
{
    private const int SystemDefaultLanguageSelectedIndex = 0;
    private const int KoreanLanguageSelectedIndex = 1;
    private const int EnglishLanguageSelectedIndex = 2;
    private const int JapaneseLanguageSelectedIndex = 3;
    private const int SimplifiedChineseLanguageSelectedIndex = 4;
    private const int TraditionalChineseLanguageSelectedIndex = 5;
    private const int SystemDefaultThemeSelectedIndex = 0;
    private const int LightThemeSelectedIndex = 1;
    private const int DarkThemeSelectedIndex = 2;

    public string ApplicationVersionText { get; } = $"v{GetCurrentApplicationVersion()}";

    public string AuthorText { get; } = localizationService.GetLocalizedString("SettingsPage_AuthorText");

    public string CheckForUpdatesLoadingMessage => localizationService.GetLocalizedString("SettingsPage_CheckForUpdatesLoadingMessage");

    [ObservableProperty]
    public partial int LanguageSelectedIndex { get; set; } = GetLanguageSelectedIndex(applicationSettings.LanguageOverride);

    [ObservableProperty]
    public partial int ThemeSelectedIndex { get; set; } = GetThemeSelectedIndex(applicationSettings.Theme);

    [ObservableProperty]
    public partial bool IsAutomaticUpdateCheckEnabled { get; set; } = applicationSettings.IsAutomaticUpdateCheckEnabled;

    [ObservableProperty]
    public partial bool IsCheckForUpdatesButtonEnabled { get; set; } = true;

    public void Load() => SynchronizePropertiesFromSettings();

    public async Task<SettingsPageDialogData> ApplyLanguageSelectedIndexAsync(int selectedIndex)
    {
        try
        {
            var languageOverride = GetLanguageOverrideFromSelectedIndex(selectedIndex);
            if (applicationSettings.LanguageOverride == languageOverride) return null;
            LanguageSelectedIndex = selectedIndex;
            applicationSettings.LanguageOverride = languageOverride;
            localizationService.ApplyLanguageTag(languageOverride);
            return SaveSettings() ? new SettingsPageDialogData(localizationService.GetLocalizedString("SettingsPage_LanguageRestartDialogTitle"), localizationService.GetLocalizedString("SettingsPage_LanguageRestartDialogMessage"), localizationService.GetLocalizedString("DialogOkButton"), ShouldNavigateToSettingsAfterClose: true) : CreateSaveSettingsFailedDialogData();
        }
        catch { return CreateSaveSettingsFailedDialogData(); }
    }

    public async Task<SettingsPageDialogData> ApplyThemeSelectedIndexAsync(int selectedIndex)
    {
        try
        {
            var theme = GetThemeFromSelectedIndex(selectedIndex);
            if (applicationSettings.Theme == theme) return null;
            ThemeSelectedIndex = selectedIndex;
            applicationSettings.Theme = theme;
            applicationThemeService.ApplyTheme(theme);
            return SaveSettings() ? null : CreateSaveSettingsFailedDialogData();
        }
        catch { return CreateSaveSettingsFailedDialogData(); }
    }

    public async Task<SettingsPageDialogData> ApplyAutomaticUpdateCheckEnabledAsync(bool isAutomaticUpdateCheckEnabled) => await SaveSettingAsync(() =>
    {
        IsAutomaticUpdateCheckEnabled = isAutomaticUpdateCheckEnabled;
        applicationSettings.IsAutomaticUpdateCheckEnabled = isAutomaticUpdateCheckEnabled;
    });

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckForUpdatesButtonEnabled = false;
        MainWindow.ShowLoading(CheckForUpdatesLoadingMessage);
        SettingsPageUpdateCheckResult updateCheckResult;
        try
        {
            var availableUpdateCount = await storeUpdateService.GetAvailableUpdateCountAsync();
            if (availableUpdateCount > 0) updateCheckResult = new SettingsPageUpdateCheckResult(localizationService.GetLocalizedString("SettingsPage_UpdateAvailableDialogTitle"), localizationService.GetFormattedString("SettingsPage_UpdateAvailableDialogMessageFormat", availableUpdateCount, ApplicationVersionText), localizationService.GetLocalizedString("SettingsPage_OpenStoreButtonText"), localizationService.GetLocalizedString("DialogCancelButton"), true);
            else updateCheckResult = new SettingsPageUpdateCheckResult(localizationService.GetLocalizedString("SettingsPage_NoUpdateDialogTitle"), localizationService.GetFormattedString("SettingsPage_NoUpdateDialogMessageFormat", ApplicationVersionText), null, null, false);
        }
        catch { updateCheckResult = new SettingsPageUpdateCheckResult(localizationService.GetLocalizedString("SettingsPage_UpdateCheckFailedDialogTitle"), localizationService.GetLocalizedString("SettingsPage_UpdateCheckFailedDialogMessage"), null, null, false); }
        finally
        {
            MainWindow.HideLoading();
            IsCheckForUpdatesButtonEnabled = true;
        }

        var contentDialogResult = await dialogService.ShowDialogAsync(updateCheckResult.Title, updateCheckResult.Message, updateCheckResult.PrimaryButtonText, updateCheckResult.SecondaryButtonText);
        if (updateCheckResult.ShouldOpenStoreAfterDialog && contentDialogResult == ContentDialogResult.Primary) await storeUpdateService.OpenStoreProductPageAsync();
    }

    [RelayCommand]
    private async Task OpenGitHubRepositoryAsync() => _ = await Launcher.LaunchUriAsync(new System.Uri("https://github.com/airtaxi/AT-Installer"));

    private void SynchronizePropertiesFromSettings()
    {
        LanguageSelectedIndex = GetLanguageSelectedIndex(applicationSettings.LanguageOverride);
        ThemeSelectedIndex = GetThemeSelectedIndex(applicationSettings.Theme);
        IsAutomaticUpdateCheckEnabled = applicationSettings.IsAutomaticUpdateCheckEnabled;
    }

    private Task<SettingsPageDialogData> SaveSettingAsync(Action applySetting)
    {
        applySetting();
        return Task.FromResult(SaveSettings() ? null : CreateSaveSettingsFailedDialogData());
    }

    private bool SaveSettings()
    {
        try
        {
            applicationSettingsService.SaveSettings();
            return true;
        }
        catch { return false; }
    }

    private SettingsPageDialogData CreateSaveSettingsFailedDialogData() => new(localizationService.GetLocalizedString("SettingsPage_SaveSettingsFailedDialogTitle"), localizationService.GetLocalizedString("SettingsPage_SaveSettingsFailedDialogMessage"));

    private static int GetLanguageSelectedIndex(string languageOverride) => languageOverride switch { "ko-KR" => KoreanLanguageSelectedIndex, "en-US" => EnglishLanguageSelectedIndex, "ja-JP" => JapaneseLanguageSelectedIndex, "zh-Hans" => SimplifiedChineseLanguageSelectedIndex, "zh-Hant" => TraditionalChineseLanguageSelectedIndex, _ => SystemDefaultLanguageSelectedIndex };

    private static string GetLanguageOverrideFromSelectedIndex(int selectedIndex) => selectedIndex switch { KoreanLanguageSelectedIndex => "ko-KR", EnglishLanguageSelectedIndex => "en-US", JapaneseLanguageSelectedIndex => "ja-JP", SimplifiedChineseLanguageSelectedIndex => "zh-Hans", TraditionalChineseLanguageSelectedIndex => "zh-Hant", _ => "" };

    private static int GetThemeSelectedIndex(ElementTheme theme) => theme switch { ElementTheme.Light => LightThemeSelectedIndex, ElementTheme.Dark => DarkThemeSelectedIndex, _ => SystemDefaultThemeSelectedIndex };

    private static ElementTheme GetThemeFromSelectedIndex(int selectedIndex) => selectedIndex switch { LightThemeSelectedIndex => ElementTheme.Light, DarkThemeSelectedIndex => ElementTheme.Dark, _ => ElementTheme.Default };

    private static string GetCurrentApplicationVersion() => FormatCurrentApplicationVersion(Package.Current.Id.Version);

    private static string FormatCurrentApplicationVersion(PackageVersion packageVersion) => $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
}