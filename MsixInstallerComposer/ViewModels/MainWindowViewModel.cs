using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Windows.Globalization;

namespace MsixInstallerComposer.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsEnglishLanguageChecked { get; set; }

    [ObservableProperty]
    public partial bool IsKoreanLanguageChecked { get; set; }

    [ObservableProperty]
    public partial bool IsJapaneseLanguageChecked { get; set; }

    [ObservableProperty]
    public partial bool IsSimplifiedChineseLanguageChecked { get; set; }

    [ObservableProperty]
    public partial bool IsTraditionalChineseLanguageChecked { get; set; }

    public MainWindowViewModel() => UpdateLanguageMenuFlyoutItems();

    [RelayCommand]
    private async Task ChangeLanguageAsync(string selectedLanguageTag)
    {
        if (LanguageTagsMatch(GetCurrentLanguageTag(), selectedLanguageTag)) return;

        ApplicationLanguages.PrimaryLanguageOverride = selectedLanguageTag;
        UpdateLanguageMenuFlyoutItems();

        await Task.CompletedTask;
    }

    private void UpdateLanguageMenuFlyoutItems()
    {
        var currentLanguageTag = GetCurrentLanguageTag();
        IsEnglishLanguageChecked = LanguageTagsMatch(currentLanguageTag, "en-US");
        IsKoreanLanguageChecked = LanguageTagsMatch(currentLanguageTag, "ko-KR");
        IsJapaneseLanguageChecked = LanguageTagsMatch(currentLanguageTag, "ja-JP");
        IsSimplifiedChineseLanguageChecked = LanguageTagsMatch(currentLanguageTag, "zh-Hans");
        IsTraditionalChineseLanguageChecked = LanguageTagsMatch(currentLanguageTag, "zh-Hant");
    }

    private static string GetCurrentLanguageTag()
    {
        var primaryLanguageOverride = ApplicationLanguages.PrimaryLanguageOverride;
        if (!string.IsNullOrWhiteSpace(primaryLanguageOverride)) return primaryLanguageOverride;
        return ApplicationLanguages.Languages[0] ?? "en-US";
    }

    private static bool LanguageTagsMatch(string currentLanguageTag, string supportedLanguageTag)
    {
        var normalizedCurrentLanguageTag = NormalizeLanguageTagForMenu(currentLanguageTag);
        var normalizedSupportedLanguageTag = NormalizeLanguageTagForMenu(supportedLanguageTag);
        if (string.Equals(normalizedCurrentLanguageTag, normalizedSupportedLanguageTag, StringComparison.OrdinalIgnoreCase)) return true;
        if (normalizedSupportedLanguageTag.StartsWith("zh-", StringComparison.OrdinalIgnoreCase)) return false;

        var currentLanguagePrefix = normalizedCurrentLanguageTag.Split('-')[0];
        var supportedLanguagePrefix = normalizedSupportedLanguageTag.Split('-')[0];
        return string.Equals(currentLanguagePrefix, supportedLanguagePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLanguageTagForMenu(string languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag)) return "en-US";

        var languageTagSegments = languageTag.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (languageTagSegments.Length == 0) return "en-US";
        if (!string.Equals(languageTagSegments[0], "zh", StringComparison.OrdinalIgnoreCase)) return languageTag;

        var isTraditionalChinese = languageTagSegments.Any(segment => string.Equals(segment, "Hant", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "TW", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "HK", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "MO", StringComparison.OrdinalIgnoreCase));
        if (isTraditionalChinese) return "zh-Hant";

        var isSimplifiedChinese = languageTagSegments.Any(segment => string.Equals(segment, "Hans", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "CN", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "SG", StringComparison.OrdinalIgnoreCase));
        if (isSimplifiedChinese) return "zh-Hans";

        return "zh-Hans";
    }
}