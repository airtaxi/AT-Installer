using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace MsixInstallerComposer.Services;

public sealed class ApplicationNotificationService(LocalizationService localizationService)
{
    public void ShowStoreUpdateAvailableNotification(int availableUpdateCount, Uri storeProductPageAddress)
    {
        var notificationTitle = localizationService.GetLocalizedString("Notification_StoreUpdateAvailableTitle");
        var notificationMessage = localizationService.GetFormattedString("Notification_StoreUpdateAvailableMessageFormat", availableUpdateCount);
        var notificationButton = new AppNotificationButton(localizationService.GetLocalizedString("Notification_OpenStoreButtonText")).SetInvokeUri(storeProductPageAddress);
        ShowNotification(notificationTitle, notificationMessage, notificationButton);
    }

    private static void ShowNotification(string notificationTitle, string notificationMessage, AppNotificationButton notificationButton = null)
    {
        try
        {
            if (!AppNotificationManager.IsSupported()) return;

            var appNotificationBuilder = new AppNotificationBuilder()
                .AddText(notificationTitle)
                .AddText(notificationMessage);

            if (notificationButton is not null) appNotificationBuilder.AddButton(notificationButton);

            AppNotificationManager.Default.Show(appNotificationBuilder.BuildNotification());
        }
        catch { }
    }
}