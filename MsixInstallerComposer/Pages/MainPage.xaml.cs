using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.Storage.Pickers;
using MsixInstallerComposer.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using PickerLocationId = Microsoft.Windows.Storage.Pickers.PickerLocationId;

namespace MsixInstallerComposer.Pages;

public sealed partial class MainPage : Page
{
    private const string MsixExtension = ".msix";
    private const string MsixBundleExtension = ".msixbundle";

    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();

        DataContext = ViewModel;

        ViewModel.ShowLoadingRequested = () => DispatcherQueue.TryEnqueue(() => MainWindow.ShowLoading());
        ViewModel.HideLoadingRequested = () => DispatcherQueue.TryEnqueue(() => MainWindow.HideLoading());
        ViewModel.UpdateLoadingMessageRequested = message => DispatcherQueue.TryEnqueue(() => MainWindow.ShowLoading(message));
        ViewModel.ShowMessageRequested = message => DispatcherQueue.TryEnqueue(() => ShowMessageAsync(message));
        ViewModel.SaveFileRequested = SaveGeneratedFileAsync;
    }

    private async void OnOpenButtonClicked(object sender, RoutedEventArgs e) => await OpenMsixFilePickerAsync();

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

            if (msixFile is StorageFile storageFile) ViewModel.LoadMsixFile(storageFile.Path);
        }
        finally { deferral.Complete(); }
    }

    private async void OnGenerateButtonClicked(object sender, RoutedEventArgs e) => await ViewModel.GenerateCommand.ExecuteAsync(null);

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

    public async Task OpenMsixFilePickerAsync()
    {
        var fileOpenPicker = new FileOpenPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
        fileOpenPicker.FileTypeFilter.Add(MsixExtension);
        fileOpenPicker.FileTypeFilter.Add(MsixBundleExtension);
        fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await fileOpenPicker.PickSingleFileAsync();
        if (file != null) ViewModel.LoadMsixFile(file.Path);
    }

    private async Task<bool> SaveGeneratedFileAsync(string sourceFilePath, string suggestedFileName)
    {
        var extension = Path.GetExtension(suggestedFileName);
        var fileSavePicker = new FileSavePicker(XamlRoot.ContentIslandEnvironment.AppWindowId);

        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)) fileSavePicker.FileTypeChoices.Add("ZIP Archive", [".zip"]);
        else fileSavePicker.FileTypeChoices.Add("Executable", [".exe"]);

        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.SuggestedFileName = suggestedFileName;

        var targetFile = await fileSavePicker.PickSaveFileAsync();
        if (targetFile == null) return false;

        await MoveFileWithRetryAsync(sourceFilePath, targetFile.Path);

        return true;
    }

    private static async Task MoveFileWithRetryAsync(string sourceFilePath, string targetFilePath, int maxRetries = 5)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
                File.Move(sourceFilePath, targetFilePath);
                return;
            }
            catch (IOException)
            {
                if (attempt == maxRetries - 1) throw;
                await Task.Delay(200);
            }
        }
    }

    private async void ShowMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "MSIX Installer Composer",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }
}