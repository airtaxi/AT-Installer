using MsixInstallerComposer.Shared.Enums;
using MsixInstallerComposerCommandLine.Services;
using System.CommandLine;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Commands;

public static class ExePackCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var inputOption = new Option<string>("--input", "-i") { Description = "Source .msix or .msixbundle file.", Required = true };
        var outputOption = new Option<string>("--output", "-o") { Description = "Output file path (.exe or .zip).", Required = true };
        var archOption = new Option<string[]>("--arch", "-a") { Description = "Target architecture: x64, x86, arm64 (repeatable). Defaults to auto-detect.", AllowMultipleArgumentsPerToken = true };

        var command = new Command("exe-pack", "Create SFX EXE installer(s) from an existing MSIX/MSIXBundle.");
        command.Options.Add(inputOption);
        command.Options.Add(outputOption);
        command.Options.Add(archOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var inputPath = parseResult.GetValue(inputOption);
            var outputPath = parseResult.GetValue(outputOption);
            var archs = parseResult.GetValue(archOption);

            var exeComposerService = serviceProvider.GetService(typeof(ExeComposerService)) as ExeComposerService;
            var progress = new ConsoleComposerProgress();

            try
            {
                var selectedArchitectures = new List<MsixArchitecture>();

                if (archs is not null)
                {
                    foreach (var arch in archs)
                    {
                        var architecture = arch.ToLowerInvariant() switch
                        {
                            "x64" or "amd64" => MsixArchitecture.X64,
                            "x86" or "win32" => MsixArchitecture.X86,
                            "arm64" or "aarch64" => MsixArchitecture.Arm64,
                            _ => throw new InvalidOperationException($"Unknown architecture: {arch}")
                        };
                        if (!selectedArchitectures.Contains(architecture)) selectedArchitectures.Add(architecture);
                    }
                }

                var result = await exeComposerService.ComposeAsync(inputPath, selectedArchitectures, outputPath, progress);
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