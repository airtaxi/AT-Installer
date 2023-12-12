using System.Diagnostics;

namespace InstallerLauncher;

internal class Program
{
	private const string PackageFileName = "Package.atp";
    private const string InstallerDirectoryName = "Installer";

    private static void Main(string[] _)
	{
		var rootPath = Path.GetDirectoryName(Environment.ProcessPath);
		var installerDirectoryPath = Path.Combine(rootPath, InstallerDirectoryName);
		var executablePath = Path.Combine(installerDirectoryPath, "Installer.exe");
		var packageFilePath = Path.Combine(rootPath, PackageFileName);

		Process.Start(executablePath, "\"" + packageFilePath + "\"");
		Environment.Exit(0);
	}
}