using MsixInstallerComposer.Shared.Helpers;
using MsixInstallerComposer.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Services;

public sealed class MsixPackagingService(WinAppBinaryService winAppBinaryService)
{
    private const string TempFolderName = "MsixInstallerComposerTemp";
    private const string DefaultPassword = "password";
    private const string LogoFileName = "logo";
    private const string PackageAppxManifestFileName = "Package.appxmanifest";

    public async Task<string> PackAsync(AticMsixConfig manifestConfig, string certificateFilePath, string certificatePassword, string version, List<string> outputFolders, string outputPath, IProgress<string> progress = null)
    {
        string publisherName = null;

        if (!string.IsNullOrWhiteSpace(certificateFilePath))
        {
            try
            {
                var password = string.IsNullOrWhiteSpace(certificatePassword) ? DefaultPassword : certificatePassword;
                var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificateFilePath, password);
                publisherName = certificate.SubjectName.Name;
            }
            catch (Exception) { throw new InvalidOperationException("Failed to load the certificate. Check the password and file path."); }
        }

        await winAppBinaryService.EnsureWinAppBinaryAsync(progress);

        var winAppBinaryPath = winAppBinaryService.GetWinAppBinaryPath();
        var workDirectoryPath = Path.Combine(Path.GetTempPath(), TempFolderName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectoryPath);

        progress?.Report("Preparing work directory...");

        string logoFileName = null;

        if (manifestConfig.LogoFileData is not null && !string.IsNullOrWhiteSpace(manifestConfig.LogoFileExtension))
        {
            logoFileName = $"{LogoFileName}{manifestConfig.LogoFileExtension}";
            var logoFilePath = Path.Combine(workDirectoryPath, logoFileName);
            await File.WriteAllBytesAsync(logoFilePath, manifestConfig.LogoFileData);
        }

        if (!Version.TryParse(version, out _)) throw new InvalidOperationException($"Invalid version format: {version}");

        var architectureFolders = new List<(string Architecture, string FolderPath)>();
        var totalFiles = 0;
        foreach (var folder in outputFolders) totalFiles += Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length;
        var copiedFiles = 0;

        await Parallel.ForEachAsync(outputFolders, (folder, token) =>
        {
            var executablePath = FindFileRecursive(folder, manifestConfig.ExecutableFileName);
            if (executablePath is null) throw new InvalidOperationException($"Executable '{manifestConfig.ExecutableFileName}' not found in folder: {folder}");

            var architecture = PeArchitectureDetector.DetectArchitecture(executablePath);
            if (architecture is "Unknown") throw new InvalidOperationException($"Unknown PE architecture for executable: {executablePath}");

            if (architectureFolders.Any(item => string.Equals(item.Architecture, architecture, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException($"Duplicate architecture: {architecture}");

            var architectureFolder = Path.Combine(workDirectoryPath, architecture);
            Directory.CreateDirectory(architectureFolder);
            CopyDirectoryRecursive(folder, architectureFolder, filesCopied =>
            {
                Interlocked.Add(ref copiedFiles, filesCopied);
                var percentage = totalFiles > 0 ? (int)(copiedFiles * 100.0 / totalFiles) : 0;
                progress?.Report($"Copying {architecture} files... {percentage}%");
            });
            architectureFolders.Add((architecture, architectureFolder));

            return ValueTask.CompletedTask;
        });

        foreach (var (architecture, architectureFolder) in architectureFolders)
        {
            progress?.Report($"Generating manifest for {architecture}...");

            var arguments = $"manifest generate {ProcessHelper.EscapeCommandLineArgument(architectureFolder)} --package-name {ProcessHelper.EscapeCommandLineArgument(manifestConfig.DisplayName)} --version {version} --description {ProcessHelper.EscapeCommandLineArgument(manifestConfig.ApplicationDescription)} --template packaged --if-exists Overwrite";

            if (publisherName is not null) arguments += $" --publisher-name {ProcessHelper.EscapeCommandLineArgument(publisherName)}";

            if (logoFileName is not null) arguments += $" --logo-path {ProcessHelper.EscapeCommandLineArgument($"..\\{logoFileName}")}";

            var exitCode = await Task.Run(() => ProcessHelper.RunCommand(winAppBinaryPath, arguments, architectureFolder, progress is not null ? progress.Report : null));

            if (exitCode != 0) throw new InvalidOperationException($"manifest generate failed for {architecture} with exit code {exitCode}");

            var appxManifestPath = Path.Combine(architectureFolder, PackageAppxManifestFileName);
            if (!File.Exists(appxManifestPath)) throw new InvalidOperationException($"Package.appxmanifest not found for {architecture}");

            FixAppxManifestDisplayName(appxManifestPath, manifestConfig.DisplayName);
        }

        progress?.Report("Packaging MSIX...");

        var packArguments = "pack";
        foreach (var(_, architectureFolder)in architectureFolders) packArguments += $" {ProcessHelper.EscapeCommandLineArgument(Path.GetFileName(architectureFolder))}";

        packArguments += $" --executable {ProcessHelper.EscapeCommandLineArgument(manifestConfig.ExecutableFileName)}";

        if (!string.IsNullOrWhiteSpace(certificateFilePath))
        {
            packArguments += $" --cert {ProcessHelper.EscapeCommandLineArgument(certificateFilePath)}";
            var password = string.IsNullOrWhiteSpace(certificatePassword) ? DefaultPassword : certificatePassword;
            packArguments += $" --cert-password {ProcessHelper.EscapeCommandLineArgument(password)}";
        }
        else packArguments += " --generate-cert";

        var packExitCode = await Task.Run(() => ProcessHelper.RunCommand(winAppBinaryPath, packArguments, workDirectoryPath, progress is not null ? progress.Report : null));

        if (packExitCode != 0) throw new InvalidOperationException($"pack failed with exit code {packExitCode}");

        var isBundle = architectureFolders.Count > 1;
        var searchPattern = isBundle ? "*.msixbundle" : "*.msix";
        var packageFiles = Directory.GetFiles(workDirectoryPath, searchPattern, SearchOption.TopDirectoryOnly);

        if (packageFiles.Length == 0) throw new InvalidOperationException("No MSIX package was found after packing.");

        var generatedPackagePath = packageFiles[0];
        var targetDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

        progress?.Report("Saving package...");
        await Task.Run(() => File.Move(generatedPackagePath, outputPath, true));

        try
        {
            if (Directory.Exists(workDirectoryPath))
            {
                Directory.Delete(workDirectoryPath, recursive: true);
            }
        }
        catch { }

        progress?.Report($"Package saved to: {outputPath}");

        return outputPath;
    }

    private static void FixAppxManifestDisplayName(string appxManifestPath, string displayName)
    {
        var content = File.ReadAllText(appxManifestPath);
        var escapedDisplayName = SecurityElement.Escape(displayName);
        var fixedContent = Regex.Replace(content, @"<DisplayName>.*?</DisplayName>", _ => $"<DisplayName>{escapedDisplayName}</DisplayName>", RegexOptions.None);
        fixedContent = Regex.Replace(fixedContent, @"(<uap:VisualElements\b[^>]*?DisplayName="")[^""]*("")", match => $"{match.Groups[1].Value}{escapedDisplayName}{match.Groups[2].Value}", RegexOptions.Singleline);
        File.WriteAllText(appxManifestPath, fixedContent);
    }

    private static string FindFileRecursive(string directoryPath, string fileName)
    {
        try { return Directory.GetFiles(directoryPath, fileName, SearchOption.AllDirectories).FirstOrDefault(); }
        catch { return null; }
    }

    private static void CopyDirectoryRecursive(string sourcePath, string destinationPath, Action<int> progress)
    {
        foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, directory);
            var targetPath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(targetPath);
        }

        var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
        var copied = 0;
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var targetPath = Path.Combine(destinationPath, relativePath);
            var directoryName = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directoryName)) Directory.CreateDirectory(directoryName);

            File.Copy(file, targetPath, overwrite: true);
            copied++;
            progress?.Invoke(1);
        }
    }
}