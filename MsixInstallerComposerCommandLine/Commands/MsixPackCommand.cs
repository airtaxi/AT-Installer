using MsixInstallerComposerCommandLine.Services;
using System.CommandLine;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Commands;

public static class MsixPackCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var manifestOption = new Option<string>("--manifest", "-m") { Description = "Path to .aticmsixconfig manifest file.", Required = true };
        var outputOption = new Option<string>("--output", "-o") { Description = "Output MSIX/MSIXBundle file path.", Required = true };
        var certOption = new Option<string>("--cert") { Description = "Path to PFX certificate file. If omitted, --generate-cert is used." };
        var certPasswordOption = new Option<string>("--cert-password") { Description = "Certificate password." };
        var versionOption = new Option<string>("--version") { Description = "Package version (e.g. \"1.0.0.0\").", DefaultValueFactory = _ => "1.0.0.0" };
        var outputFolderOption = new Option<string[]>("--output-folder", "-f") { Description = "Architecture build output folder (repeatable).", Required = true, AllowMultipleArgumentsPerToken = true };

        var command = new Command("msix-pack", "Package MSIX/MSIXBundle from a manifest config file and output folders.");
        command.Options.Add(manifestOption);
        command.Options.Add(outputOption);
        command.Options.Add(certOption);
        command.Options.Add(certPasswordOption);
        command.Options.Add(versionOption);
        command.Options.Add(outputFolderOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var manifestPath = parseResult.GetValue(manifestOption);
            var outputPath = parseResult.GetValue(outputOption);
            var certPath = parseResult.GetValue(certOption);
            var certPassword = parseResult.GetValue(certPasswordOption);
            var version = parseResult.GetValue(versionOption);
            var outputFolders = parseResult.GetValue(outputFolderOption);

            var manifestService = serviceProvider.GetService(typeof(ManifestService)) as ManifestService;
            var packagingService = serviceProvider.GetService(typeof(MsixPackagingService)) as MsixPackagingService;
            var progress = new ConsoleProgress();

            try
            {
                var manifestConfig = await manifestService.LoadAsync(manifestPath);
                var result = await packagingService.PackAsync(manifestConfig, certPath, certPassword, version, [.. outputFolders], outputPath, progress);
                Console.Out.WriteLine($"Generated: {result}");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Error: {exception.Message}");
                return 1;
            }
        });

        return command;
    }
}