using MsixInstallerComposer.Shared.Enums;
using MsixInstallerComposer.Shared.Helpers;
using MsixInstallerComposer.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Services;

public sealed class ExeComposerService
{
    private const string CacheFolderName = "MsixInstallerComposer";
    private const string SfxSubFolderName = "SFX";
    private const string TempFolderName = "MsixInstallerComposerTemp";
    private const string ZipFileName = "Release.zip";

    private static string LocalAppDataPath => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public async Task<string> ComposeAsync(string msixFilePath, List<MsixArchitecture> selectedArchitectures, string outputPath, IProgress<ComposerProgress> progress = null)
    {
        progress?.Report(new ComposerProgress { Message = "Detecting architecture...", Stage = ComposerProgressStage.DetectingArchitecture });

        var architectureInfo = MsixArchitectureDetector.Detect(msixFilePath);
        var targetArchitectures = selectedArchitectures.Count > 0 ? selectedArchitectures.Intersect(architectureInfo.Architectures).ToList() : architectureInfo.Architectures;
        if (targetArchitectures.Count == 0) throw new InvalidOperationException("No matching architectures found between the selected and the MSIX package.");

        var sfxRootPath = Path.Combine(LocalAppDataPath, CacheFolderName, SfxSubFolderName);
        var tempRootPath = Path.Combine(Path.GetTempPath(), TempFolderName, Guid.NewGuid().ToString());

        Directory.CreateDirectory(sfxRootPath);
        Directory.CreateDirectory(tempRootPath);

        try
        {
            progress?.Report(new ComposerProgress { Message = "Checking latest SFX release...", Stage = ComposerProgressStage.DownloadingTemplate });

            var releaseTag = await SfxTemplateDownloader.GetLatestReleaseTagAsync();
            var versionCachePath = Path.Combine(sfxRootPath, releaseTag);
            var zipFilePath = Path.Combine(versionCachePath, ZipFileName);

            var isCacheValid = Directory.Exists(versionCachePath) && !File.Exists(zipFilePath) && File.Exists(Path.Combine(versionCachePath, "bz.exe"));

            if (isCacheValid) progress?.Report(new ComposerProgress { Message = "Using cached SFX template...", Stage = ComposerProgressStage.ExtractingTemplate });
            else
            {
                progress?.Report(new ComposerProgress { Message = "Downloading SFX template from GitHub...", Stage = ComposerProgressStage.DownloadingTemplate });

                if (Directory.Exists(versionCachePath)) Directory.Delete(versionCachePath, recursive: true);

                Directory.CreateDirectory(versionCachePath);

                var downloadProgress = new Progress<DownloadProgress>(report => progress?.Report(new ComposerProgress { Message = $"Downloading SFX template... {report.Percentage}%", Stage = ComposerProgressStage.DownloadingTemplate }));
                await SfxTemplateDownloader.DownloadAsync(versionCachePath, releaseTag, downloadProgress);

                progress?.Report(new ComposerProgress { Message = "Extracting SFX template...", Stage = ComposerProgressStage.ExtractingTemplate });

                ZipFile.ExtractToDirectory(zipFilePath, versionCachePath, overwriteFiles: true);

                File.Delete(zipFilePath);
            }

            progress?.Report(new ComposerProgress { Message = "Composing installers...", Stage = ComposerProgressStage.Composing });

            var packageExtension = architectureInfo.IsBundle ? ".msixbundle" : ".msix";
            var packageFileName = $"Package{packageExtension}";
            var configFileName = architectureInfo.IsBundle ? "config_msixbundle.txt" : "config_msix.txt";
            var bzExeName = GetBzExeName();

            var generatedFiles = new List<string>();

            foreach (var architecture in targetArchitectures)
            {
                var (archiveFolderName, installerFileName, sfxModuleName, configName) = GetArchitectureInfo(architecture);

                var archiveFolderPath = Path.Combine(versionCachePath, archiveFolderName);
                var targetPackagePath = Path.Combine(archiveFolderPath, packageFileName);

                File.Copy(msixFilePath, targetPackagePath, overwrite: true);

                var archiveSevenZPath = Path.Combine(versionCachePath, $"{archiveFolderName}.7z");
                if (File.Exists(archiveSevenZPath)) File.Delete(archiveSevenZPath);

                progress?.Report(new ComposerProgress { Message = $"Archiving {architecture} files...", Stage = ComposerProgressStage.Composing });

                var createArchiveResult = ProcessHelper.RunCommand(Path.Combine(versionCachePath, bzExeName), $"c -storeroot:no {ProcessHelper.EscapeCommandLineArgument(archiveSevenZPath)} {ProcessHelper.EscapeCommandLineArgument(archiveFolderPath)}", versionCachePath, onOutput: line => progress?.Report(new ComposerProgress { Message = $"Archiving {architecture}: {line}", Stage = ComposerProgressStage.Composing }));
                if (createArchiveResult != 0) throw new InvalidOperationException($"Failed to create archive for {architecture}.");

                var composeConfigPath = Path.Combine(versionCachePath, configName);
                File.Copy(Path.Combine(versionCachePath, configFileName), composeConfigPath, overwrite: true);

                progress?.Report(new ComposerProgress { Message = $"Composing {architecture} installer...", Stage = ComposerProgressStage.Composing });

                var composeResult = ProcessHelper.RunCommand("cmd.exe", $"/c copy /b /y {ProcessHelper.EscapeCommandLineArgument(sfxModuleName)} + {ProcessHelper.EscapeCommandLineArgument(configName)} + {ProcessHelper.EscapeCommandLineArgument($"{archiveFolderName}.7z")} {ProcessHelper.EscapeCommandLineArgument(installerFileName)}", versionCachePath);
                if (composeResult != 0) throw new InvalidOperationException($"Failed to compose installer for {architecture}.");

                var generatedInstallerPath = Path.Combine(versionCachePath, installerFileName);
                var tempInstallerPath = Path.Combine(tempRootPath, installerFileName);
                File.Move(generatedInstallerPath, tempInstallerPath, overwrite: true);

                generatedFiles.Add(tempInstallerPath);
            }

            var isMultiple = generatedFiles.Count > 1;
            string finalFilePath;

            if (isMultiple)
            {
                progress?.Report(new ComposerProgress { Message = "Creating ZIP archive...", Stage = ComposerProgressStage.Composing });

                var tempZipPath = Path.Combine(tempRootPath, Path.GetFileNameWithoutExtension(outputPath) + ".zip");
                ZipFile.CreateFromDirectory(tempRootPath, tempZipPath);

                foreach (var file in generatedFiles) File.Delete(file);

                finalFilePath = tempZipPath;
            }
            else finalFilePath = generatedFiles[0];

            var targetDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            await Task.Run(() => File.Move(finalFilePath, outputPath, true));

            progress?.Report(new ComposerProgress { Message = $"Installer saved to: {outputPath}", Stage = ComposerProgressStage.Done });

            return outputPath;
        }
        catch
        {
            CleanupDirectory(tempRootPath);
            throw;
        }
    }

    private static string GetBzExeName() => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "bz-arm64.exe" : "bz.exe";

    private static (string ArchiveFolderName, string InstallerFileName, string SfxModuleName, string ConfigName) GetArchitectureInfo(MsixArchitecture architecture)
    {
        return architecture switch
        {
            MsixArchitecture.X64 => ("Archive", "Installer-x64.exe", "7zS.sfx", "config_x64.txt"),
            MsixArchitecture.X86 => ("Archive-x86", "Installer-x86.exe", "7zS-x86.sfx", "config_x86.txt"),
            MsixArchitecture.Arm64 => ("Archive-arm64", "Installer-arm64.exe", "7zS-arm64.sfx", "config_arm64.txt"),
            _ => throw new ArgumentOutOfRangeException(nameof(architecture))
        };
    }

    private static void CleanupDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch { }
    }
}