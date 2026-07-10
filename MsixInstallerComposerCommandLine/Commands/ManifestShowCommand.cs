using MsixInstallerComposerCommandLine.Services;
using System.CommandLine;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Commands;

public static class ManifestShowCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var inputArgument = new Argument<string>("input-path") { Description = "Path to .aticmsixconfig file." };

        var command = new Command("show", "Display manifest config file contents.");
        command.Arguments.Add(inputArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var inputPath = parseResult.GetValue(inputArgument);

            var manifestService = serviceProvider.GetService(typeof(ManifestService)) as ManifestService;

            try
            {
                var config = await manifestService.LoadAsync(inputPath);
                Console.Out.WriteLine($"Version:          {config.Version}");
                Console.Out.WriteLine($"Display Name:     {config.DisplayName}");
                Console.Out.WriteLine($"Description:      {config.ApplicationDescription}");
                Console.Out.WriteLine($"Executable:       {config.ExecutableFileName}");
                Console.Out.WriteLine($"Logo:             {(config.LogoFileData is not null ? $"Yes ({config.LogoFileData.Length} bytes, {config.LogoFileExtension})" : "None")}");
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