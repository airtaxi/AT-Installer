using InstallerCommons;

namespace InstallerComposerCommandLine;

public static class Program
{
    public static int Main(string[] commandLineArguments)
    {
        if (commandLineArguments.Length != 1)
        {
            WriteUsage();
            return 64;
        }

        try
        {
            var configurationFilePath = Path.GetFullPath(commandLineArguments[0]);
            var installerComposerConfiguration = InstallerComposerConfigurationFile.LoadConfiguration(configurationFilePath);
            ResolveConfigurationPaths(installerComposerConfiguration, configurationFilePath);

            var compositionProgress = new ConsoleCompositionProgress();
            InstallerPackageComposer.CreatePackage(installerComposerConfiguration, compositionProgress);

            Console.WriteLine($"Package exported: {installerComposerConfiguration.PackageFilePath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }

    private static void ResolveConfigurationPaths(InstallerComposerConfiguration installerComposerConfiguration, string configurationFilePath)
    {
        var configurationDirectoryPath = Path.GetDirectoryName(configurationFilePath);
        if (string.IsNullOrWhiteSpace(configurationDirectoryPath)) configurationDirectoryPath = Directory.GetCurrentDirectory();

        if (!string.IsNullOrWhiteSpace(installerComposerConfiguration.ApplicationRootDirectoryPath) && !Path.IsPathFullyQualified(installerComposerConfiguration.ApplicationRootDirectoryPath)) installerComposerConfiguration.ApplicationRootDirectoryPath = Path.GetFullPath(Path.Combine(configurationDirectoryPath, installerComposerConfiguration.ApplicationRootDirectoryPath));
        if (!string.IsNullOrWhiteSpace(installerComposerConfiguration.PackageFilePath) && !Path.IsPathFullyQualified(installerComposerConfiguration.PackageFilePath)) installerComposerConfiguration.PackageFilePath = Path.GetFullPath(Path.Combine(configurationDirectoryPath, installerComposerConfiguration.PackageFilePath));
    }

    private static void WriteUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  InstallerComposerCommandLine <path-to-config.aticconfig>");
    }

    private sealed class ConsoleCompositionProgress : IProgress<string>
    {
        public void Report(string message) => Console.WriteLine(message);
    }
}
