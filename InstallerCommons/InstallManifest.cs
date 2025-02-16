namespace InstallerCommons;

public class InstallManifest
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Publisher { get; set; }
	public Version Version { get; set; }
	public byte[] IconBinary { get; set; }

	public string ArchiveFileName { get; set; }
	public string ExecutableFileName { get; set; }
	public string InstallationFolderName { get; set; }
	public string CommitSha { get; set; } // Optional
    public string ExecuteAfterInstall { get; set; } // Optional
	public string ExecuteAfterReinstall { get; set; } // Optional
	public string ExecuteOnUninstall { get; set; } // Optional
}