using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MsixInstallerComposer.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MsixInstallerComposer.Services;

public sealed class DialogService(LocalizationService localizationService, ApplicationThemeService applicationThemeService)
{
    public async Task<ContentDialogResult> ShowDialogAsync(string title, string message, string primaryButtonText = null, string secondaryButtonText = null)
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return ContentDialogResult.None;

        HideOpenContentDialogs(xamlRoot);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText ?? localizationService.GetLocalizedString("DialogHelper_ConfirmButtonText"),
            XamlRoot = xamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        applicationThemeService.RegisterThemeTarget(dialog);

        if (!string.IsNullOrEmpty(secondaryButtonText)) dialog.SecondaryButtonText = secondaryButtonText;
        return await dialog.ShowAsync();
    }

    public async Task ShowMessageAsync(string title, string message, string closeButtonText = null)
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return;

        HideOpenContentDialogs(xamlRoot);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeButtonText ?? localizationService.GetLocalizedString("DialogHelper_ConfirmButtonText"),
            XamlRoot = xamlRoot,
            DefaultButton = ContentDialogButton.Close
        };
        applicationThemeService.RegisterThemeTarget(dialog);
        await dialog.ShowAsync();
    }

    private static void HideOpenContentDialogs(XamlRoot xamlRoot)
    {
        var contentDialogs = VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot).Where(popup => popup.Child is ContentDialog).Select(popup => popup.Child as ContentDialog);
        if (!contentDialogs.Any()) return;

        foreach (var contentDialog in contentDialogs) contentDialog.Hide();
    }
}