using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using MsixInstallerComposer.Helpers;
using MsixInstallerComposer.Models;
using MsixInstallerComposer.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MsixInstallerComposer.ViewModels;

public sealed partial class PackagingPageViewModel(LocalizationService localizationService, DialogService dialogService, PickerService pickerService, WinAppService winAppService) : ObservableObject
{
    private const string TempFolderName = "MsixInstallerComposerTemp";
    private const string DefaultPassword = "password";
    private const string LogoFileName = "logo";
    private const string PackageAppxManifestFileName = "Package.appxmanifest";

    private AticMsixConfig _manifestConfig;
    private string _certificateFilePath;
    private string DefaultVersion => $"{Major}.{Minor}.{Build}.{Revision}";

    [ObservableProperty]
    public partial string ManifestFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CertificateFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CertificatePassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCertificate { get; set; }

    [ObservableProperty]
    public partial bool HasManifest { get; set; }

    [ObservableProperty]
    public partial bool IsOutputFolderEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsOutputFolderExpanded { get; set; }

    [ObservableProperty]
    public partial string Major { get; set; } = "1";

    [ObservableProperty]
    public partial string Minor { get; set; } = "0";

    [ObservableProperty]
    public partial string Build { get; set; } = "0";

    [ObservableProperty]
    public partial string Revision { get; set; } = "0";

    [ObservableProperty]
    public partial string VersionPreview { get; set; } = "1.0.0.0";

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    public ObservableCollection<OutputFolderItemViewModel> OutputFolders { get; } = [];

    public bool CanGenerate => HasManifest && OutputFolders.Count > 0 && !IsGenerating && IsValidVersion();

    partial void OnHasManifestChanged(bool value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnIsGeneratingChanged(bool value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnMajorChanged(string value) => UpdateVersionPreview();

    partial void OnMinorChanged(string value) => UpdateVersionPreview();

    partial void OnBuildChanged(string value) => UpdateVersionPreview();

    partial void OnRevisionChanged(string value) => UpdateVersionPreview();

    private void UpdateVersionPreview()
    {
        VersionPreview = $"{Major}.{Minor}.{Build}.{Revision}";
        OnPropertyChanged(nameof(CanGenerate));
    }

    private bool IsValidVersion() => int.TryParse(Major, out _) && int.TryParse(Minor, out _) && int.TryParse(Build, out _) && int.TryParse(Revision, out _);

    [RelayCommand]
    private async Task LoadManifestFileAsync()
    {
        var filePath = await pickerService.PickOpenAticMsixConfigFileAsync();
        if (filePath is null) return;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize(json, AticMsixConfigSerializerContext.Default.AticMsixConfig);

            if (config is null)
            {
                await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_ManifestLoadFailedMessage"));
                return;
            }

            _manifestConfig = config;
            ManifestFilePath = filePath;
            HasManifest = true;
            IsOutputFolderEnabled = true;
        }
        catch (Exception exception) { await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackagingPage_ManifestLoadFailedMessageFormat", exception.Message)); }
    }

    [RelayCommand]
    private async Task LoadCertificateFileAsync()
    {
        var filePath = await pickerService.PickOpenPfxFileAsync();
        if (filePath is null) return;

        _certificateFilePath = filePath;
        CertificateFilePath = filePath;
        HasCertificate = true;
    }

    [RelayCommand]
    private async Task AddOutputFolderAsync()
    {
        if (_manifestConfig is null) return;

        var folderPath = await pickerService.PickFolderAsync();
        if (folderPath is null) return;

        var executableFileName = _manifestConfig.ExecutableFileName;
        if (string.IsNullOrWhiteSpace(executableFileName))
        {
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_ExecutableFileNameMissingMessage"));
            return;
        }

        var foundExecutablePath = FindFileRecursive(folderPath, executableFileName);
        if (foundExecutablePath is null)
        {
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackagingPage_ExecutableNotFoundMessageFormat", executableFileName, folderPath));
            return;
        }

        var normalizedFolderPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (OutputFolders.Any(item => string.Equals(Path.GetFullPath(item.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalizedFolderPath, StringComparison.OrdinalIgnoreCase)))
        {
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_DuplicateFolderPathMessage"));
            return;
        }

        var architecture = PeArchitectureDetector.DetectArchitecture(foundExecutablePath);

        if (architecture is "Unknown")
        {
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_UnknownArchitectureMessage"));
            return;
        }

        if (OutputFolders.Any(item => string.Equals(item.Architecture, architecture, StringComparison.OrdinalIgnoreCase)))
        {
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackagingPage_DuplicateArchitectureMessageFormat", architecture));
            return;
        }

        OutputFolders.Add(new OutputFolderItemViewModel(OutputFolders, architecture, folderPath));
        if (!IsOutputFolderExpanded) IsOutputFolderExpanded = true;

        OnPropertyChanged(nameof(CanGenerate));
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (_manifestConfig is null || OutputFolders.Count == 0) return;

        if (!IsValidVersion())
        {
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_InvalidVersionMessage"));
            return;
        }

        string publisherName = null;

        if (HasCertificate && !string.IsNullOrWhiteSpace(_certificateFilePath))
        {
            try
            {
                var password = string.IsNullOrWhiteSpace(CertificatePassword) ? DefaultPassword : CertificatePassword;
                var certificate = X509CertificateLoader.LoadPkcs12FromFile(_certificateFilePath, password);
                publisherName = certificate.SubjectName.Name;
            }
            catch (Exception)
            {
                await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_InvalidCertificatePasswordMessage"));
                return;
            }
        }

        IsGenerating = true;
        string workDirectoryPath = null;
        try
        {
            await winAppService.EnsureWinAppBinaryAsync();

            MainWindow.ShowLoading(localizationService.GetLocalizedString("PackagingPage_PreparingWorkDirectoryMessage"));

            var winAppBinaryPath = winAppService.GetWinAppBinaryPath();
            workDirectoryPath = Path.Combine(Path.GetTempPath(), TempFolderName, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDirectoryPath);

            string logoFileName = null;

            if (_manifestConfig.LogoFileData is not null && !string.IsNullOrWhiteSpace(_manifestConfig.LogoFileExtension))
            {
                logoFileName = $"{LogoFileName}{_manifestConfig.LogoFileExtension}";
                var logoFilePath = Path.Combine(workDirectoryPath, logoFileName);
                await File.WriteAllBytesAsync(logoFilePath, _manifestConfig.LogoFileData);
            }

            var version = DefaultVersion;
            var isVersionValid = Version.TryParse(version, out var _);
            if (!isVersionValid)
            {
                MainWindow.HideLoading();
                IsGenerating = false;
                await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_InvalidVersionFormatMessage"));
                return;
            }

            var outputFoldersList = OutputFolders.ToList();

            var totalFiles = 0;
            var copiedFiles = 0;

            foreach (var folder in outputFoldersList) totalFiles += Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories).Length;

            var architectureFolders = new System.Collections.Generic.List<(string Architecture, string FolderPath)>();

            await Parallel.ForEachAsync(outputFoldersList, (folder, token) =>
            {
                var architectureFolder = Path.Combine(workDirectoryPath, folder.Architecture);
                Directory.CreateDirectory(architectureFolder);
                CopyDirectoryRecursive(folder.Path, architectureFolder, progress =>
                {
                    copiedFiles += progress;
                    var percentage = totalFiles > 0 ? (int)(copiedFiles * 100.0 / totalFiles) : 0;
                    MainWindow.ShowLoading(localizationService.GetFormattedString("PackagingPage_CopyingFilesMessageFormat", folder.Architecture, percentage));
                });
                architectureFolders.Add((folder.Architecture, architectureFolder));

                return ValueTask.CompletedTask;
            });

            foreach (var (architecture, architectureFolder) in architectureFolders)
            {
                MainWindow.ShowLoading(localizationService.GetFormattedString("PackagingPage_GeneratingManifestMessageFormat", architecture));

                var arguments = $"manifest generate \"{architectureFolder}\" --package-name \"{_manifestConfig.DisplayName}\" --version {version} --description \"{_manifestConfig.ApplicationDescription}\" --template packaged --if-exists Overwrite";

                if (publisherName is not null) arguments += $" --publisher-name \"{publisherName}\"";

                if (logoFileName is not null) arguments += $" --logo-path \"..\\{logoFileName}\"";

                var exitCode = await Task.Run(() => ProcessHelper.RunCommand(winAppBinaryPath, arguments, architectureFolder));

                if (exitCode != 0)
                {
                    MainWindow.HideLoading();
                    await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackagingPage_ManifestGenerateFailedMessageFormat", architecture, exitCode));
                    IsGenerating = false;
                    return;
                }

                var appxManifestPath = Path.Combine(architectureFolder, PackageAppxManifestFileName);
                if (!File.Exists(appxManifestPath))
                {
                    MainWindow.HideLoading();
                    await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackagingPage_AppxManifestNotFoundMessageFormat", architecture));
                    IsGenerating = false;
                    return;
                }

                FixAppxManifestDisplayName(appxManifestPath, _manifestConfig.DisplayName);
            }

            MainWindow.ShowLoading(localizationService.GetLocalizedString("PackagingPage_PackagingMessage"));

            var packArguments = "pack";
            foreach (var(_, architectureFolder)in architectureFolders) packArguments += $" \"{Path.GetFileName(architectureFolder)}\"";

            packArguments += $" --executable \"{_manifestConfig.ExecutableFileName}\"";

            if (HasCertificate && !string.IsNullOrWhiteSpace(_certificateFilePath))
            {
                packArguments += $" --cert \"{_certificateFilePath}\"";
                var password = string.IsNullOrWhiteSpace(CertificatePassword) ? DefaultPassword : CertificatePassword;
                packArguments += $" --cert-password \"{password}\"";
            }
            else packArguments += " --generate-cert";

            var packExitCode = await Task.Run(() => ProcessHelper.RunCommand(winAppBinaryPath, packArguments, workDirectoryPath));

            if (packExitCode != 0)
            {
                MainWindow.HideLoading();
                await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackagingPage_PackFailedMessageFormat", packExitCode));
                IsGenerating = false;
                return;
            }

            var isBundle = architectureFolders.Count > 1;
            var searchPattern = isBundle ? "*.msixbundle" : "*.msix";
            var packageFiles = Directory.GetFiles(workDirectoryPath, searchPattern, SearchOption.TopDirectoryOnly);

            if (packageFiles.Length == 0)
            {
                MainWindow.HideLoading();
                await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_PackageNotFoundMessage"));
                IsGenerating = false;
                return;
            }

            var generatedPackagePath = packageFiles[0];
            var defaultFileName = Path.GetFileName(generatedPackagePath);

            var targetFilePath = await pickerService.PickSaveMsixFileAsync(defaultFileName, isBundle);
            if (targetFilePath is null)
            {
                MainWindow.HideLoading();
                IsGenerating = false;
                return;
            }

            MainWindow.ShowLoading(localizationService.GetLocalizedString("PackagingPage_SavingPackageMessage"));
            await Task.Run(() => File.Move(generatedPackagePath, targetFilePath, true));
        }
        catch (Exception exception)
        {
            MainWindow.HideLoading();
            IsGenerating = false;
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackagingPage_GenerateFailedMessageFormat", exception.Message));
            return;
        }
        finally
        {
            if (!string.IsNullOrEmpty(workDirectoryPath)) await CleanupDirectoryAsync(workDirectoryPath, progress => MainWindow.ShowLoading(localizationService.GetFormattedString("PackagingPage_CleaningUpMessageFormat", progress.Percentage)));

            // If success, IsGenerating is still true
            if (IsGenerating)
            {
                MainWindow.HideLoading();
                IsGenerating = false;
                await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackagingPage_GenerateSuccessMessage"));
            }
        }
    }

    private static async Task CleanupDirectoryAsync(string directoryPath, Action<DeleteProgress> progress = null)
    {
        try
        {
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            var totalFiles = files.Length;
            var deletedFiles = 0;

            await Parallel.ForEachAsync(files, async (file, _) =>
            {
                try { File.Delete(file); }
                catch { }
                Interlocked.Increment(ref deletedFiles);
                progress?.Invoke(new DeleteProgress { DeletedFiles = deletedFiles, TotalFiles = totalFiles });
                await ValueTask.CompletedTask;
            });

            Directory.Delete(directoryPath, recursive: true);
        }
        catch { }
    }

    private static void FixAppxManifestDisplayName(string appxManifestPath, string displayName)
    {
        var content = File.ReadAllText(appxManifestPath);
        var pattern = @"<DisplayName>.*?</DisplayName>";
        var escapedDisplayName = SecurityElement.Escape(displayName);
        var fixedContent = Regex.Replace(content, pattern, _ => $"<DisplayName>{escapedDisplayName}</DisplayName>", RegexOptions.None);
        File.WriteAllText(appxManifestPath, fixedContent);
    }

    private static string FindFileRecursive(string directoryPath, string fileName)
    {
        try
        {
            var foundPath = Directory.GetFiles(directoryPath, fileName, SearchOption.AllDirectories).FirstOrDefault();
            return foundPath;
        }
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