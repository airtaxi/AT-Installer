namespace InstallerCommons;

public class UninstallManifest
{
	public InstallManifest InstallManifest { get; set; }

	public Version InstalledVersion { get; set; }

	public string ExecuteOnUninstall { get; set; } // Optional
}