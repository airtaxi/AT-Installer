using MsixInstallerComposerCommandLine.Services;
using System.CommandLine;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Commands;

public static class CertGenerateCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var publisherOption = new Option<string>("--publisher") { Description = "Publisher name or X.500 distinguished name.", Required = true };
        var validDaysOption = new Option<int>("--valid-days") { Description = "Certificate validity in days.", DefaultValueFactory = _ => 1825 };
        var passwordOption = new Option<string>("--password") { Description = "PFX password. Defaults to \"password\"." };
        var outputOption = new Option<string>("--output", "-o") { Description = "Output PFX file path.", Required = true };

        var command = new Command("generate", "Generate a self-signed PFX certificate.");
        command.Options.Add(publisherOption);
        command.Options.Add(validDaysOption);
        command.Options.Add(passwordOption);
        command.Options.Add(outputOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var publisher = parseResult.GetValue(publisherOption);
            var validDays = parseResult.GetValue(validDaysOption);
            var password = parseResult.GetValue(passwordOption);
            var outputPath = parseResult.GetValue(outputOption);

            var certificateService = serviceProvider.GetService(typeof(CertificateService)) as CertificateService;
            var progress = new ConsoleProgress();

            try
            {
                var result = await certificateService.GenerateAsync(publisher, validDays, password, outputPath, progress);
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