using Microsoft.UI.Xaml.Controls;

namespace InstallerComposer;

public static class DialogHelper
{
    public static async Task<ContentDialogResult> ShowDialogAsync(this UIElement element, string title, string content, string primaryButtonText, string secondaryButtonText = null)
    {
        var dialog = new ContentDialog()
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            XamlRoot = element.XamlRoot
        };

        return await dialog.ShowAsync();
    }
}
