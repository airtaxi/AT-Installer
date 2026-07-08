using MsixInstallerComposer.Helpers;
using MsixInstallerComposer.Pages;
using MsixInstallerComposer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUIEx;
using TitleBar = Microsoft.UI.Xaml.Controls.TitleBar;

namespace MsixInstallerComposer;

public sealed partial class MainWindow : WindowEx
{
    private static MainWindow s_instance;

    public MainWindowViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        s_instance = this;

        InitializeComponent();

        AppWindow.SetIcon("Assets/Icon.ico");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        this.CenterOnScreen();

        AppFrame.Navigate(typeof(MainPage));
    }

    public static void ShowLoading(string message = null) =>
        s_instance.DispatcherQueue.TryEnqueue(() => { s_instance.AppFrame.IsEnabled = false; if (string.IsNullOrWhiteSpace(message)) s_instance.LoadingTextBlock.Visibility = Visibility.Collapsed; else { s_instance.LoadingTextBlock.Visibility = Visibility.Visible; s_instance.LoadingTextBlock.Text = message; }  s_instance.LoadingGrid.Visibility = Visibility.Visible; });

    public static void HideLoading() => s_instance.DispatcherQueue.TryEnqueue(() => { s_instance.LoadingGrid.Visibility = Visibility.Collapsed; s_instance.AppFrame.IsEnabled = true; });

    private void OnAppFrameNavigated(object sender, NavigationEventArgs e)
    {
        var frame = sender as Frame;

        AppTitleBar.IsBackButtonVisible = frame.CanGoBack;
    }

    private void OnAppTitleBarBackRequested(TitleBar sender, object args)
    {
        if (AppFrame.CanGoBack)
        {
            AppFrame.GoBack();
        }
    }

    private async void OnLoadMsixMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
    {
        if (AppFrame.Content is MainPage mainPage)
        {
            await mainPage.OpenMsixFilePickerAsync();
        }
    }

    private void OnExitMenuFlyoutItemClicked(object sender, RoutedEventArgs e) => Close();

    private async void OnLanguageRadioMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem selectedLanguageMenuFlyoutItem) return;
        if (selectedLanguageMenuFlyoutItem.Tag is not string selectedLanguageTag) return;

        await ViewModel.ChangeLanguageCommand.ExecuteAsync(selectedLanguageTag);

        var resourceLoader = new ResourceLoader();
        await Content.ShowDialogAsync(resourceLoader.GetString("LanguageChangeDialogContent"), resourceLoader.GetString("LanguageChangeDialogTitle"), resourceLoader.GetString("DialogOkButton"));
    }

    private async void OnAboutMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
    {
        var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var resourceLoader = new ResourceLoader();
        var aboutContent = string.Format(resourceLoader.GetString("AboutDialogContent"), localVersion);
        await Content.ShowDialogAsync(resourceLoader.GetString("AboutDialogTitle"), aboutContent, resourceLoader.GetString("DialogOkButton"));
    }
}