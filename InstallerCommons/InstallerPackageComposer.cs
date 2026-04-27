using InstallerCommons.ZipHelper;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace InstallerCommons;

public static class InstallerPackageComposer
{
    private static readonly char[] s_invalidWindowsFileNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static void ValidateConfiguration(InstallerComposerConfiguration installerComposerConfiguration)
    {
        ArgumentNullException.ThrowIfNull(installerComposerConfiguration);

        var isValidApplicationId = Guid.TryParse(installerComposerConfiguration.ApplicationId, out _);
        if (!isValidApplicationId) throw new InvalidDataException("The Application ID field is not a valid GUID.");

        var isValidApplicationName = !string.IsNullOrWhiteSpace(installerComposerConfiguration.ApplicationName);
        if (!isValidApplicationName) throw new InvalidDataException("The Application Name field is empty.");

        var isValidApplicationPublisher = !string.IsNullOrWhiteSpace(installerComposerConfiguration.ApplicationPublisher);
        if (!isValidApplicationPublisher) throw new InvalidDataException("The Application Publisher field is empty.");

        var isValidApplicationRootDirectoryPath = !string.IsNullOrWhiteSpace(installerComposerConfiguration.ApplicationRootDirectoryPath) && Directory.Exists(installerComposerConfiguration.ApplicationRootDirectoryPath);
        if (!isValidApplicationRootDirectoryPath) throw new DirectoryNotFoundException("The Application Root Directory field is empty or the directory does not exist.");

        var applicationExecutableFilePath = Path.Combine(installerComposerConfiguration.ApplicationRootDirectoryPath, installerComposerConfiguration.ApplicationExecutableFileName ?? string.Empty);
        var isValidApplicationExecutableFile = !string.IsNullOrWhiteSpace(installerComposerConfiguration.ApplicationExecutableFileName) && File.Exists(applicationExecutableFilePath);
        if (!isValidApplicationExecutableFile) throw new FileNotFoundException("Application Executable File is not selected or the file does not exist.", applicationExecutableFilePath);

        var isValidApplicationInstallationFolderName = !string.IsNullOrWhiteSpace(installerComposerConfiguration.ApplicationInstallationFolderName);
        if (!isValidApplicationInstallationFolderName) throw new InvalidDataException("The Application Installation Folder Name field is empty.");

        isValidApplicationInstallationFolderName = !ContainsInvalidWindowsFileNameCharacter(installerComposerConfiguration.ApplicationInstallationFolderName);
        if (!isValidApplicationInstallationFolderName) throw new InvalidDataException("The Application Installation Folder Name field contains illegal characters.");

        var isValidPackageFilePath = !string.IsNullOrWhiteSpace(installerComposerConfiguration.PackageFilePath);
        if (!isValidPackageFilePath) throw new InvalidDataException("The Package File Path field is not set.");

        var packageFileDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(installerComposerConfiguration.PackageFilePath));
        var isValidPackageFilePathDirectory = !string.IsNullOrWhiteSpace(packageFileDirectoryPath) && Directory.Exists(packageFileDirectoryPath);
        if (!isValidPackageFilePathDirectory) throw new DirectoryNotFoundException("The Package File Path's directory does not exist.");
    }

    public static void CreatePackage(InstallerComposerConfiguration installerComposerConfiguration, IProgress<string> compositionProgress = null)
    {
        ValidateConfiguration(installerComposerConfiguration);

        var applicationExecutableFilePath = Path.Combine(installerComposerConfiguration.ApplicationRootDirectoryPath, installerComposerConfiguration.ApplicationExecutableFileName);
        var applicationVersionInformation = FileVersionInfo.GetVersionInfo(applicationExecutableFilePath);
        var applicationExecutableFileVersionText = applicationVersionInformation.FileVersion;
        if (string.IsNullOrWhiteSpace(applicationExecutableFileVersionText)) throw new InvalidDataException("The Application Executable File has no file version.");
        var applicationExecutableFileVersion = new Version(applicationExecutableFileVersionText);

        var installManifest = new InstallManifest()
        {
            Id = installerComposerConfiguration.ApplicationId,
            Name = installerComposerConfiguration.ApplicationName,
            Publisher = installerComposerConfiguration.ApplicationPublisher,
            IconBinary = installerComposerConfiguration.ApplicationIconBinary,
            ArchiveFileName = "data.bin",
            ExecutableFileName = installerComposerConfiguration.ApplicationExecutableFileName,
            InstallationFolderName = installerComposerConfiguration.ApplicationInstallationFolderName,
            Version = applicationExecutableFileVersion,
            ExecuteAfterInstall = string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteAfterInstall) ? null : installerComposerConfiguration.ExecuteAfterInstall,
            ExecuteAfterReinstall = string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteAfterReinstall) ? null : installerComposerConfiguration.ExecuteAfterReinstall,
            ExecuteOnUninstall = string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteOnUninstall) ? null : installerComposerConfiguration.ExecuteOnUninstall
        };

        var exportInstanceIdentifier = Guid.NewGuid().ToString();
        var temporaryDirectoryPath = Path.Combine(Path.GetTempPath(), "ATInstallerComposer");
        var instanceDirectoryPath = Path.Combine(temporaryDirectoryPath, exportInstanceIdentifier);
        var temporaryPackageFilePath = Path.Combine(temporaryDirectoryPath, $"{exportInstanceIdentifier}.atp");
        Directory.CreateDirectory(instanceDirectoryPath);

        try
        {
            compositionProgress?.Report("Exporting Manifest...");
            File.WriteAllText(Path.Combine(instanceDirectoryPath, "manifest.json"), JsonSerializer.Serialize(installManifest, SourceGenerationContext.Default.InstallManifest));

            compositionProgress?.Report("Generating Uninstall Manifest...");
            var uninstallManifestJson = CreateUninstallManifestJson(installerComposerConfiguration.ApplicationRootDirectoryPath, installManifest, applicationExecutableFileVersion);

            compositionProgress?.Report("Archiving Application Root Directory...");
            var archiveFilePath = Path.Combine(instanceDirectoryPath, "data.bin");
            CreateApplicationArchive(installerComposerConfiguration.ApplicationRootDirectoryPath, archiveFilePath, uninstallManifestJson, compositionProgress);

            compositionProgress?.Report("Exporting Package...");
            ZipFileNative.CreateFromDirectory(instanceDirectoryPath, temporaryPackageFilePath, CompressionLevel.Optimal, new ActionProgress<ZipProgressStatus>(packageProgress =>
            {
                compositionProgress?.Report($"Exporting Package... ({packageProgress.Progress:P0})");
            }));

            compositionProgress?.Report("Finishing...");
            File.Move(temporaryPackageFilePath, installerComposerConfiguration.PackageFilePath, true);
        }
        finally
        {
            compositionProgress?.Report("Cleaning Up...");
            if (Directory.Exists(instanceDirectoryPath)) Directory.Delete(instanceDirectoryPath, true);
            if (File.Exists(temporaryPackageFilePath)) File.Delete(temporaryPackageFilePath);
        }
    }

    private static string CreateUninstallManifestJson(string applicationRootDirectoryPath, InstallManifest installManifest, Version applicationExecutableFileVersion)
    {
        var allFiles = Directory.GetFiles(applicationRootDirectoryPath, "*.*", SearchOption.AllDirectories);
        var installedFiles = allFiles.Select(filePath => Path.GetRelativePath(applicationRootDirectoryPath, filePath).Replace('\\', '/')).ToList();
        var uninstallManifest = new UninstallManifest()
        {
            InstallManifest = installManifest,
            InstalledVersion = applicationExecutableFileVersion,
            ExecuteOnUninstall = installManifest.ExecuteOnUninstall,
            InstalledFiles = installedFiles
        };
        return JsonSerializer.Serialize(uninstallManifest, SourceGenerationContext.Default.UninstallManifest);
    }

    private static void CreateApplicationArchive(string applicationRootDirectoryPath, string archiveFilePath, string uninstallManifestJson, IProgress<string> compositionProgress)
    {
        var uninstallManifestFilePath = Path.Combine(applicationRootDirectoryPath, "uninstall.json");
        var hasExistingUninstallManifest = File.Exists(uninstallManifestFilePath);
        var existingUninstallManifestBinary = hasExistingUninstallManifest ? File.ReadAllBytes(uninstallManifestFilePath) : null;

        try
        {
            File.WriteAllText(uninstallManifestFilePath, uninstallManifestJson);
            ZipFileNative.CreateFromDirectory(applicationRootDirectoryPath, archiveFilePath, CompressionLevel.NoCompression, new ActionProgress<ZipProgressStatus>(archiveProgress =>
            {
                compositionProgress?.Report($"Archiving Application Root Directory... ({archiveProgress.Progress:P0})\n{archiveProgress.FileName}");
            }));
        }
        finally
        {
            if (hasExistingUninstallManifest) File.WriteAllBytes(uninstallManifestFilePath, existingUninstallManifestBinary);
            else if (File.Exists(uninstallManifestFilePath)) File.Delete(uninstallManifestFilePath);
        }
    }

    private static bool ContainsInvalidWindowsFileNameCharacter(string value) => value.Any(character => character < 32 || s_invalidWindowsFileNameCharacters.Contains(character));
}
