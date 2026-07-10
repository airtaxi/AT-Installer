using MsixInstallerComposerCommandLine.Services;
using System.CommandLine;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Commands;

public static class ManifestUpdateCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var inputArgument = new Argument<string>("input-path") { Description = "Path to .aticmsixconfig file to update." };
        var displayNameOption = new Option<string>("--display-name") { Description = "New display name." };
        var descriptionOption = new Option<string>("--description") { Description = "New application description." };
        var executableOption = new Option<string>("--executable") { Description = "New executable file name." };
        var logoOption = new Option<string>("--logo") { Description = "Path to new logo image file." };
        var removeLogoOption = new Option<bool>("--remove-logo") { Description = "Remove the logo from the manifest." };
        var outputOption = new Option<string>("--output", "-o") { Description = "Output path. Defaults to overwriting the input file." };

        var command = new Command("update", "Update an existing .aticmsixconfig manifest file.");
        command.Arguments.Add(inputArgument);
        command.Options.Add(displayNameOption);
        command.Options.Add(descriptionOption);
        command.Options.Add(executableOption);
        command.Options.Add(logoOption);
        command.Options.Add(removeLogoOption);
        command.Options.Add(outputOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var inputPath = parseResult.GetValue(inputArgument);
            var displayName = parseResult.GetValue(displayNameOption);
            var description = parseResult.GetValue(descriptionOption);
            var executable = parseResult.GetValue(executableOption);
            var logo = parseResult.GetValue(logoOption);
            var removeLogo = parseResult.GetValue(removeLogoOption);
            var outputPath = parseResult.GetValue(outputOption);

            var manifestService = serviceProvider.GetService(typeof(ManifestService)) as ManifestService;
            var progress = new ConsoleProgress();

            try
            {
                var result = await manifestService.UpdateAsync(inputPath, displayName, description, executable, logo, removeLogo, outputPath, progress);
                Console.Out.WriteLine($"Updated: {result}");
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