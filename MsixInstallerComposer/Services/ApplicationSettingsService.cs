using Microsoft.UI.Xaml;
using MsixInstallerComposer.Models;
using System;
using Windows.Storage;

namespace MsixInstallerComposer.Services;

public sealed class ApplicationSettingsService
{
    private const string ThemeSettingKey = "Theme";
    private const string LanguageOverrideSettingKey = "LanguageOverride";
    private const string IsAutomaticUpdateCheckEnabledSettingKey = "IsAutomaticUpdateCheckEnabled";

    public ApplicationSettingsService() => Settings = LoadSettings();

    public event EventHandler SettingsChanged;

    public ApplicationSettings Settings { get; }

    public void SaveSettings()
    {
        NormalizeSettings(Settings);
        var localSettings = ApplicationData.Current.LocalSettings;
        localSettings.Values[ThemeSettingKey] = Settings.Theme.ToString();
        localSettings.Values[LanguageOverrideSettingKey] = Settings.LanguageOverride ?? "";
        localSettings.Values[IsAutomaticUpdateCheckEnabledSettingKey] = Settings.IsAutomaticUpdateCheckEnabled;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ApplicationSettings LoadSettings()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        var theme = ElementTheme.Default;
        if (localSettings.Values.TryGetValue(ThemeSettingKey, out var storedTheme) && storedTheme is string themeString && Enum.TryParse(themeString, true, out theme)) { }
        var languageOverride = "";
        if (localSettings.Values.TryGetValue(LanguageOverrideSettingKey, out var storedLanguage) && storedLanguage is string languageString) languageOverride = languageString;
        var isAutomaticUpdateCheckEnabled = true;
        if (localSettings.Values.TryGetValue(IsAutomaticUpdateCheckEnabledSettingKey, out var storedAutoCheck) && storedAutoCheck is bool autoCheckValue) isAutomaticUpdateCheckEnabled = autoCheckValue;

        var applicationSettings = new ApplicationSettings
        {
            Theme = theme,
            LanguageOverride = languageOverride,
            IsAutomaticUpdateCheckEnabled = isAutomaticUpdateCheckEnabled
        };
        NormalizeSettings(applicationSettings);
        return applicationSettings;
    }

    private static void NormalizeSettings(ApplicationSettings applicationSettings)
    {
        applicationSettings.Theme = applicationSettings.Theme is ElementTheme.Default or ElementTheme.Light or ElementTheme.Dark ? applicationSettings.Theme : ElementTheme.Default;
        applicationSettings.LanguageOverride = NormalizeLanguageOverride(applicationSettings.LanguageOverride);
    }

    private static string NormalizeLanguageOverride(string languageOverride) => languageOverride is "ko-KR" or "en-US" or "ja-JP" or "zh-Hans" or "zh-Hant" ? languageOverride : "";
}