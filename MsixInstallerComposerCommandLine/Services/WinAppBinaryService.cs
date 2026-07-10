using MsixInstallerComposer.Shared.Helpers;
using MsixInstallerComposer.Shared.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Services;

public sealed class WinAppBinaryService
{
    private const string CacheFolderName = "MsixInstallerComposer";
    private const string WinAppSubFolderName = "winapp";
    private const string ZipFileName = "cli-binaries.zip";
    private const string ExeFileName = "winapp.exe";

    private static string LocalAppDataPath => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string GetWinAppBinaryPath()
    {
        var cachePath = Path.Combine(LocalAppDataPath, CacheFolderName);
        var winAppPath = Path.Combine(cachePath, WinAppSubFolderName);
        return Path.Combine(winAppPath, ExeFileName);
    }

    public async Task EnsureWinAppBinaryAsync(IProgress<string> progress = null)
    {
        var cachePath = Path.Combine(LocalAppDataPath, CacheFolderName);
        var winAppPath = Path.Combine(cachePath, WinAppSubFolderName);
        var zipFilePath = Path.Combine(cachePath, ZipFileName);
        var exeFilePath = Path.Combine(winAppPath, ExeFileName);

        progress?.Report("Checking winapp.exe binary...");

        if (!File.Exists(zipFilePath) && File.Exists(exeFilePath)) return;

        if (RuntimeInformation.ProcessArchitecture == Architecture.X86) progress?.Report("Warning: x86 process architecture may not be supported by winapp.exe.");

        progress?.Report("Downloading winapp.exe...");

        var downloadProgress = new ConsoleDownloadProgress();
        await WinAppCliBinaryDownloader.DownloadAsync(cachePath, downloadProgress);

        progress?.Report("Extracting winapp.exe...");

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

        progress?.Report("winapp.exe is ready.");
    }
}