using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Threading.Tasks;
using FolderPicker = Microsoft.Windows.Storage.Pickers.FolderPicker;
using PickerLocationId = Microsoft.Windows.Storage.Pickers.PickerLocationId;

namespace MsixInstallerComposer.Services;

public sealed class PickerService(LocalizationService localizationService)
{
    private const string MsixExtension = ".msix";
    private const string MsixBundleExtension = ".msixbundle";
    private const string ZipExtension = ".zip";
    private const string ExeExtension = ".exe";
    private const string PfxExtension = ".pfx";
    private const string AticMsixConfigExtension = ".aticmsixconfig";
    private const string JpgExtension = ".jpg";
    private const string JpegExtension = ".jpeg";
    private const string PngExtension = ".png";
    private const string BmpExtension = ".bmp";
    private const string IcoExtension = ".ico";

    public async Task<string> PickOpenMsixFileAsync()
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var fileOpenPicker = new FileOpenPicker(xamlRoot.ContentIslandEnvironment.AppWindowId);
        fileOpenPicker.FileTypeFilter.Add(MsixExtension);
        fileOpenPicker.FileTypeFilter.Add(MsixBundleExtension);
        fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await fileOpenPicker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string> PickSaveFileAsync(string suggestedFileName, string extension)
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var fileSavePicker = new FileSavePicker(xamlRoot.ContentIslandEnvironment.AppWindowId);

        if (extension.Equals(ZipExtension, StringComparison.OrdinalIgnoreCase)) fileSavePicker.FileTypeChoices.Add(localizationService.GetLocalizedString("PickerService_ZipArchiveFileType"), [ZipExtension]);
        else fileSavePicker.FileTypeChoices.Add(localizationService.GetLocalizedString("PickerService_ExecutableFileType"), [ExeExtension]);

        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.SuggestedFileName = suggestedFileName;

        var targetFile = await fileSavePicker.PickSaveFileAsync();
        return targetFile?.Path;
    }

    public async Task<string> PickSavePfxFileAsync(string suggestedFileName)
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var fileSavePicker = new FileSavePicker(xamlRoot.ContentIslandEnvironment.AppWindowId);
        fileSavePicker.FileTypeChoices.Add(localizationService.GetLocalizedString("PickerService_PfxFileType"), [PfxExtension]);
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.SuggestedFileName = suggestedFileName;

        var targetFile = await fileSavePicker.PickSaveFileAsync();
        return targetFile?.Path;
    }

    public async Task<string> PickSaveAticMsixConfigFileAsync(string suggestedFileName)
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var fileSavePicker = new FileSavePicker(xamlRoot.ContentIslandEnvironment.AppWindowId);
        fileSavePicker.FileTypeChoices.Add(localizationService.GetLocalizedString("PickerService_AticMsixConfigFileType"), [AticMsixConfigExtension]);
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.SuggestedFileName = suggestedFileName;

        var targetFile = await fileSavePicker.PickSaveFileAsync();
        return targetFile?.Path;
    }

    public async Task<string> PickOpenAticMsixConfigFileAsync()
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var fileOpenPicker = new FileOpenPicker(xamlRoot.ContentIslandEnvironment.AppWindowId);
        fileOpenPicker.FileTypeFilter.Add(AticMsixConfigExtension);
        fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await fileOpenPicker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string> PickOpenLogoImageFileAsync()
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var fileOpenPicker = new FileOpenPicker(xamlRoot.ContentIslandEnvironment.AppWindowId);
        fileOpenPicker.FileTypeFilter.Add(JpgExtension);
        fileOpenPicker.FileTypeFilter.Add(JpegExtension);
        fileOpenPicker.FileTypeFilter.Add(PngExtension);
        fileOpenPicker.FileTypeFilter.Add(BmpExtension);
        fileOpenPicker.FileTypeFilter.Add(IcoExtension);
        fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var file = await fileOpenPicker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string> PickOpenPfxFileAsync()
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var fileOpenPicker = new FileOpenPicker(xamlRoot.ContentIslandEnvironment.AppWindowId);
        fileOpenPicker.FileTypeFilter.Add(PfxExtension);
        fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await fileOpenPicker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string> PickSaveMsixFileAsync(string suggestedFileName, bool isBundle)
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var fileSavePicker = new FileSavePicker(xamlRoot.ContentIslandEnvironment.AppWindowId);
        var extension = isBundle ? MsixBundleExtension : MsixExtension;
        var fileTypeLabel = isBundle ? localizationService.GetLocalizedString("PickerService_MsixBundleFileType") : localizationService.GetLocalizedString("PickerService_MsixFileType");
        fileSavePicker.FileTypeChoices.Add(fileTypeLabel, [extension]);
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.SuggestedFileName = suggestedFileName;

        var targetFile = await fileSavePicker.PickSaveFileAsync();
        return targetFile?.Path;
    }

    public async Task<string> PickFolderAsync()
    {
        var xamlRoot = MainWindow.XamlRoot;
        if (xamlRoot is null) return null;

        var folderPicker = new FolderPicker(xamlRoot.ContentIslandEnvironment.AppWindowId) { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };

        var folder = await folderPicker.PickSingleFolderAsync();
        return folder?.Path;
    }
}