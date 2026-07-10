using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MsixInstallerComposer.Messages;
using MsixInstallerComposer.Pages;
using MsixInstallerComposer.Services;
using System.Globalization;
using Windows.Globalization;
using WinUIEx;
using TitleBar = Microsoft.UI.Xaml.Controls.TitleBar;

namespace MsixInstallerComposer;

public sealed partial class MainWindow : WindowEx
{
    private static MainWindow s_instance;

    public static XamlRoot XamlRoot => s_instance?.Content?.XamlRoot;

    private readonly ApplicationThemeService _applicationThemeService = App.Services.GetRequiredService<ApplicationThemeService>();
    private readonly LocalizationService _localizationService = App.Services.GetRequiredService<LocalizationService>();

    public MainWindow()
    {
        s_instance = this;

        InitializeComponent();

        AppWindow.SetIcon("Assets/Icon.ico");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        _applicationThemeService.ApplyThemeToWindow(this);
        _applicationThemeService.ThemeChanged += OnApplicationThemeServiceThemeChanged;

        this.CenterOnScreen();

        AppFrame.Navigate(typeof(PackageExePage));

        RefreshLocalizedText();
        _localizationService.LanguageChanged += RefreshLocalizedText;
    }

    public static void ShowLoading(string message = null) => s_instance.DispatcherQueue.TryEnqueue(() =>
    {
        if (s_instance.DispatcherQueue.HasThreadAccess) SetLoadingState(Visibility.Visible, message);
        else s_instance.DispatcherQueue.TryEnqueue(() => SetLoadingState(Visibility.Visible, message));
    });

    public static void HideLoading()
    {
        if (s_instance.DispatcherQueue.HasThreadAccess) SetLoadingState(Visibility.Collapsed, null);
        else s_instance.DispatcherQueue.TryEnqueue(() => SetLoadingState(Visibility.Collapsed, null));
    }

    private static void SetLoadingState(Visibility visibility, string message)
    {
        s_instance.LoadingGrid.Visibility = visibility;
        if (!string.IsNullOrEmpty(message) || visibility == Visibility.Visible)
        {
            s_instance.AppTitleBar.IsEnabled = false;
            s_instance.AppFrame.IsEnabled = false;
            s_instance.LoadingTextBlock.Text = message;
            s_instance.LoadingTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            s_instance.AppTitleBar.IsEnabled = true;
            s_instance.AppFrame.IsEnabled = true;
            s_instance.LoadingTextBlock.Visibility = Visibility.Collapsed;
            s_instance.LoadingTextBlock.Text = "";
        }
    }

    private void RefreshLocalizedText()
    {
        var localizedWindowTitle = _localizationService.GetLocalizedString("WindowTitle.Title");
        Title = localizedWindowTitle;
        AppTitleBar.Title = localizedWindowTitle;

        PackageExeSelectorBarItem.Text = _localizationService.GetLocalizedString("PackageExeSelectorBarItem.Text");
        PackageMsixSelectorBarItem.Text = _localizationService.GetLocalizedString("PackageMsixSelectorBarItem.Text");
        SettingsSelectorBarItem.Text = _localizationService.GetLocalizedString("SettingsSelectorBarItem.Text");

        // This method can only be called from SettingsPage.
        // Reload the page to apply the new language setting.
        AppFrame.Navigate(typeof(SettingsPage));
    }

    private void OnAppTitleBarPaneToggleRequested(TitleBar sender, object args) => WeakReferenceMessenger.Default.Send(new AppTitleBarPaneToggledMessage());

    private void OnAppFrameNavigated(object sender, NavigationEventArgs navigationEventArguments) => AppTitleBar.IsPaneToggleButtonVisible = (sender as Frame).SourcePageType == typeof(PackageMsixPage);

    private void OnPageSelectorBarSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs selectorBarSelectionChangedEventArguments)
    {
        var selectedTag = sender.SelectedItem?.Tag as string;
        var targetType = selectedTag switch
        {
            "PackageExe" => typeof(PackageExePage),
            "PackageMsix" => typeof(PackageMsixPage),
            "Settings" => typeof(SettingsPage),
            _ => typeof(PackageExePage)
        };

        if (AppFrame.CurrentSourcePageType != targetType) AppFrame.Navigate(targetType);
    }

    private void OnApplicationThemeServiceThemeChanged(ElementTheme theme) => _applicationThemeService.ApplyThemeToWindow(this);

    private void OnMainWindowClosed(object sender, WindowEventArgs windowEventArguments)
    {
        _applicationThemeService.ThemeChanged -= OnApplicationThemeServiceThemeChanged;
        _localizationService.LanguageChanged -= RefreshLocalizedText;
    }
}