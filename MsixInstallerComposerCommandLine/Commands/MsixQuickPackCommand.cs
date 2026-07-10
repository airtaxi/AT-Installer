using MsixInstallerComposer.Shared.Models;
using MsixInstallerComposerCommandLine.Services;
using System.CommandLine;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Commands;

public static class MsixQuickPackCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var displayNameOption = new Option<string>("--display-name") { Description = "Application display name.", Required = true };
        var descriptionOption = new Option<string>("--description") { Description = "Application description.", Required = true };
        var executableOption = new Option<string>("--executable") { Description = "Executable file name (e.g. \"My app.exe\").", Required = true };
        var logoOption = new Option<string>("--logo") { Description = "Path to logo image file." };
        var certOption = new Option<string>("--cert") { Description = "Path to PFX certificate file. If omitted, --generate-cert is used." };
        var certPasswordOption = new Option<string>("--cert-password") { Description = "Certificate password." };
        var versionOption = new Option<string>("--version") { Description = "Package version (e.g. \"1.0.0.0\").", DefaultValueFactory = _ => "1.0.0.0" };
        var outputOption = new Option<string>("--output", "-o") { Description = "Output MSIX/MSIXBundle file path.", Required = true };
        var outputFolderOption = new Option<string[]>("--output-folder", "-f") { Description = "Architecture build output folder (repeatable).", Required = true, AllowMultipleArgumentsPerToken = true };

        var command = new Command("msix-quick-pack", "Package MSIX/MSIXBundle from inline parameters without a manifest file.");
        command.Options.Add(displayNameOption);
        command.Options.Add(descriptionOption);
        command.Options.Add(executableOption);
        command.Options.Add(logoOption);
        command.Options.Add(certOption);
        command.Options.Add(certPasswordOption);
        command.Options.Add(versionOption);
        command.Options.Add(outputOption);
        command.Options.Add(outputFolderOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var displayName = parseResult.GetValue(displayNameOption);
            var description = parseResult.GetValue(descriptionOption);
            var executable = parseResult.GetValue(executableOption);
            var logo = parseResult.GetValue(logoOption);
            var certPath = parseResult.GetValue(certOption);
            var certPassword = parseResult.GetValue(certPasswordOption);
            var version = parseResult.GetValue(versionOption);
            var outputPath = parseResult.GetValue(outputOption);
            var outputFolders = parseResult.GetValue(outputFolderOption);

            var packagingService = serviceProvider.GetService(typeof(MsixPackagingService)) as MsixPackagingService;
            var progress = new ConsoleProgress();

            try
            {
                var config = new AticMsixConfig
                {
                    Version = 1,
                    DisplayName = displayName,
                    ApplicationDescription = description,
                    ExecutableFileName = executable
                };

                if (!string.IsNullOrWhiteSpace(logo))
                {
                    var logoData = await System.IO.File.ReadAllBytesAsync(logo);
                    config.LogoFileData = logoData;
                    config.LogoFileExtension = System.IO.Path.GetExtension(logo);
                }

                var result = await packagingService.PackAsync(config, certPath, certPassword, version, [.. outputFolders], outputPath, progress);
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