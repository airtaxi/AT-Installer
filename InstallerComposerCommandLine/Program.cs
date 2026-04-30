using InstallerCommons;

namespace InstallerComposerCommandLine;

public static class Program
{
    public static int Main(string[] commandLineArguments)
    {
        if (!TryParseCommandLineArguments(commandLineArguments, out var parsedCommandLineArguments))
        {
            WriteUsage();
            return 64;
        }

        try
        {
            var configurationFilePath = Path.GetFullPath(parsedCommandLineArguments.ConfigurationFilePath);
            var installerComposerConfiguration = InstallerComposerConfigurationFile.LoadConfiguration(configurationFilePath);
            ResolveConfigurationPaths(installerComposerConfiguration, configurationFilePath);

            var compositionProgress = new ConsoleCompositionProgress(parsedCommandLineArguments.UseMinimalLogging);
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

    private static bool TryParseCommandLineArguments(string[] commandLineArguments, out ParsedCommandLineArguments parsedCommandLineArguments)
    {
        parsedCommandLineArguments = null;

        string configurationFilePath = null;
        var useMinimalLogging = false;

        foreach (var commandLineArgument in commandLineArguments)
        {
            if (string.Equals(commandLineArgument, "--minimal-log", StringComparison.OrdinalIgnoreCase))
            {
                useMinimalLogging = true;
                continue;
            }

            if (configurationFilePath is null)
            {
                configurationFilePath = commandLineArgument;
                continue;
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(configurationFilePath)) return false;

        parsedCommandLineArguments = new(configurationFilePath, useMinimalLogging);
        return true;
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
        Console.WriteLine("  InstallerComposerCommandLine [--minimal-log] <path-to-config.aticconfig>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --minimal-log  Prints only stage-level progress messages.");
    }

    private sealed class ConsoleCompositionProgress(bool useMinimalLogging) : IProgress<string>
    {
        private string _lastReportedStageMessage;

        public void Report(string message)
        {
            if (!useMinimalLogging)
            {
                Console.WriteLine(message);
                return;
            }

            var stageMessage = NormalizeStageMessage(message);
            if (string.IsNullOrWhiteSpace(stageMessage) || string.Equals(_lastReportedStageMessage, stageMessage, StringComparison.Ordinal)) return;

            _lastReportedStageMessage = stageMessage;
            Console.WriteLine(stageMessage);
        }

        private static string NormalizeStageMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return string.Empty;

            var newLineIndex = message.IndexOfAny(['\r', '\n']);
            var singleLineMessage = newLineIndex >= 0 ? message[..newLineIndex] : message;
            var progressSuffixIndex = singleLineMessage.IndexOf(" (", StringComparison.Ordinal);
            return progressSuffixIndex >= 0 ? singleLineMessage[..progressSuffixIndex] : singleLineMessage;
        }
    }

    private sealed record ParsedCommandLineArguments(string ConfigurationFilePath, bool UseMinimalLogging);
}
