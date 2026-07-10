using MsixInstallerComposer.Helpers;
using MsixInstallerComposer.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MsixInstallerComposer.Services;

public sealed class WinAppService(LocalizationService localizationService, DialogService dialogService)
{
    private const string CacheFolderName = "MsixInstallerComposer";
    private const string WinAppSubFolderName = "winapp";
    private const string ZipFileName = "cli-binaries.zip";
    private const string ExeFileName = "winapp.exe";

    public string GetWinAppBinaryPath()
    {
        var localAppDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        var cachePath = Path.Combine(localAppDataPath, CacheFolderName);
        var winAppPath = Path.Combine(cachePath, WinAppSubFolderName);
        return Path.Combine(winAppPath, ExeFileName);
    }

    public async Task EnsureWinAppBinaryAsync()
    {
        var localAppDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        var cachePath = Path.Combine(localAppDataPath, CacheFolderName);
        var winAppPath = Path.Combine(cachePath, WinAppSubFolderName);
        var zipFilePath = Path.Combine(cachePath, ZipFileName);
        var exeFilePath = Path.Combine(winAppPath, ExeFileName);

        MainWindow.ShowLoading(localizationService.GetLocalizedString("WinAppService_CheckingBinaryMessage"));

        if (!File.Exists(zipFilePath) && File.Exists(exeFilePath))
        {
            MainWindow.HideLoading();
            return;
        }

        if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
        {
            MainWindow.HideLoading();
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("WinAppService_UnsupportedArchitectureMessage"));
            return;
        }

        MainWindow.ShowLoading(localizationService.GetFormattedString("WinAppService_DownloadingMessageFormat", 0));

        var progress = new Progress<DownloadProgress>(downloadProgress => MainWindow.ShowLoading(localizationService.GetFormattedString("WinAppService_DownloadingMessageFormat", downloadProgress.Percentage)));

        await WinAppCliBinaryDownloader.DownloadAsync(cachePath, progress);

        MainWindow.ShowLoading(localizationService.GetLocalizedString("WinAppService_ExtractingMessage"));

        var architectureFolder = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";

        await Task.Run(() =>
        {
            if (Directory.Exists(winAppPath)) Directory.Delete(winAppPath, recursive: true);
            Directory.CreateDirectory(winAppPath);

            using var archive = ZipFile.OpenRead(zipFilePath);
            var prefix = architectureFolder + "/";

            foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name)))
            {
                var relativePath = entry.FullName[prefix.Length..];
                var destinationPath = Path.Combine(winAppPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        });

        if (!File.Exists(exeFilePath)) throw new InvalidOperationException($"winapp.exe not found after extraction at {exeFilePath}");

        File.Delete(zipFilePath);

        MainWindow.HideLoading();
    }
}
