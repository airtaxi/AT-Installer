using MsixInstallerComposerCommandLine.Services;
using System.CommandLine;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Commands;

public static class ManifestCreateCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var displayNameOption = new Option<string>("--display-name") { Description = "Application display name.", Required = true };
        var descriptionOption = new Option<string>("--description") { Description = "Application description.", Required = true };
        var executableOption = new Option<string>("--executable") { Description = "Executable file name (e.g. \"My app.exe\").", Required = true };
        var logoOption = new Option<string>("--logo") { Description = "Path to logo image file (jpg/png/bmp/ico)." };
        var outputOption = new Option<string>("--output", "-o") { Description = "Output .aticmsixconfig file path.", Required = true };

        var command = new Command("create", "Create a new .aticmsixconfig manifest file.");
        command.Options.Add(displayNameOption);
        command.Options.Add(descriptionOption);
        command.Options.Add(executableOption);
        command.Options.Add(logoOption);
        command.Options.Add(outputOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var displayName = parseResult.GetValue(displayNameOption);
            var description = parseResult.GetValue(descriptionOption);
            var executable = parseResult.GetValue(executableOption);
            var logo = parseResult.GetValue(logoOption);
            var outputPath = parseResult.GetValue(outputOption);

            var manifestService = serviceProvider.GetService(typeof(ManifestService)) as ManifestService;
            var progress = new ConsoleProgress();

            try
            {
                var result = await manifestService.CreateAsync(displayName, description, executable, logo, outputPath, progress);
                Console.Out.WriteLine($"Created: {result}");
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