namespace InstallerComposer.DataTypes;

public class InstallerComposerConfig
{
    public string ApplicationId { get; set; }
    public string ApplicationName { get; set; }
    public string ApplicationPublisher { get; set; }
    public string ApplicationRootDirectoryPath { get; set; }
    public string ApplicationExecutableFileName { get; set; }
    public string ApplicationInstallationFolderName { get; set; }
    public byte[] ApplicationIconBinary { get; set; }
    public string PackageFilePath { get; set; }
    public string ExecuteAfterInstall { get; set; } // Optional
    public string ExecuteOnUninstall { get; set; } // Optional
}
