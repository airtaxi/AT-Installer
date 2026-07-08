using Installer.Msix.Enum;
using Installer.Msix.Model;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace Installer.Msix;

public static class MsixDeploymentHelper
{
    public static MsixInstalledPackageInfo FindInstalledPackage(MsixPackageMetadata metadata)
    {
        if (metadata?.Name == null) return null;
        try
        {
            var packageManager = new PackageManager();
            foreach (var package in packageManager.FindPackagesForUser(string.Empty))
            {
                if (package.Id.Name != metadata.Name) continue;

                var installedVersion = new Version((int)package.Id.Version.Major, (int)package.Id.Version.Minor, (int)package.Id.Version.Build, (int)package.Id.Version.Revision);
                var status = metadata.Version == null ? MsixInstallStatus.SameVersion : CompareVersions(installedVersion, metadata.Version);

                return new MsixInstalledPackageInfo
                {
                    PackageFullName = package.Id.FullName,
                    Version = installedVersion,
                    Status = status
                };
            }
        }
        catch { }
        return null;
    }

    private static MsixInstallStatus CompareVersions(Version installed, Version target)
    {
        if (installed > target) return MsixInstallStatus.NewerVersion;
        if (installed == target) return MsixInstallStatus.SameVersion;
        return MsixInstallStatus.OlderVersion;
    }

    public static async Task<bool> RemovePackageAsync(string packageFullName)
    {
        try
        {
            var packageManager = new PackageManager();
            await packageManager.RemovePackageAsync(packageFullName).AsTask();
            return true;
        }
        catch { return false; }
    }

    public static async Task<MsixDeploymentResult> AddPackageAsync(string packageFilePath, IProgress<DeploymentProgress> progress)
    {
        var packageManager = new PackageManager();
        var packageUri = new Uri(packageFilePath);
        var deploymentOperation = packageManager.AddPackageAsync(packageUri, null, DeploymentOptions.ForceApplicationShutdown);

        try { await deploymentOperation.AsTask(progress); }
        catch (Exception exception) { return new MsixDeploymentResult { IsSuccessful = false, ErrorText = exception.Message, ErrorCode = exception.HResult }; }

        var deploymentResult = deploymentOperation.GetResults();
        return new MsixDeploymentResult
        {
            IsSuccessful = deploymentOperation.Status == AsyncStatus.Completed,
            ErrorText = deploymentResult?.ErrorText,
            ErrorCode = deploymentOperation.ErrorCode is { } errorCode ? errorCode.HResult : 0
        };
    }
}