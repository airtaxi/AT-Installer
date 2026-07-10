using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MsixInstallerComposer.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace MsixInstallerComposer.Pages;

public sealed partial class PackageExePage : Page
{
    private const string MsixExtension = ".msix";
    private const string MsixBundleExtension = ".msixbundle";

    public PackageExePageViewModel ViewModel { get; }

    public PackageExePage()
    {
        ViewModel = App.Services.GetRequiredService<PackageExePageViewModel>();

        InitializeComponent();

        DataContext = ViewModel;
    }

    private void OnMsixFileDragOver(object sender, DragEventArgs e)
    {
        var dataView = e.DataView;
        if (dataView.Contains(StandardDataFormats.StorageItems))
        {
            var deferral = e.GetDeferral();
            _ = CheckDraggedFilesAsync(dataView, items =>
            {
                if (items) e.AcceptedOperation = DataPackageOperation.Copy;
                deferral.Complete();
            });
        }
    }

    private async void OnMsixFileDrop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();
            var msixFile = items.FirstOrDefault(item => item is StorageFile file && (file.FileType.Equals(MsixExtension, StringComparison.OrdinalIgnoreCase) || file.FileType.Equals(MsixBundleExtension, StringComparison.OrdinalIgnoreCase)));

            if (msixFile is StorageFile storageFile) await ViewModel.LoadMsixFileAsync(storageFile.Path);
        }
        finally { deferral.Complete(); }
    }

    private static async Task<bool> CheckDraggedFilesAsync(DataPackageView dataView, Action<bool> callback)
    {
        try
        {
            var items = await dataView.GetStorageItemsAsync();
            var hasMsix = items.Any(item => item is StorageFile file && (file.FileType.Equals(MsixExtension, StringComparison.OrdinalIgnoreCase) || file.FileType.Equals(MsixBundleExtension, StringComparison.OrdinalIgnoreCase)));
            callback(hasMsix);
            return hasMsix;
        }
        catch
        {
            callback(false);
            return false;
        }
    }
}