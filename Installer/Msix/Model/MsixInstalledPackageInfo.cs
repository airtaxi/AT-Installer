using Installer.Msix.Enum;

namespace Installer.Msix.Model;

public sealed class MsixInstalledPackageInfo
{
    public string PackageFullName { get; set; }
    public Version Version { get; set; }
    public MsixInstallStatus Status { get; set; }
}