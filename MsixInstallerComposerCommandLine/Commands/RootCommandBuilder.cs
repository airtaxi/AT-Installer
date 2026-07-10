using System.CommandLine;

namespace MsixInstallerComposerCommandLine.Commands;

public static class RootCommandBuilder
{
    public static RootCommand Build(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("AT Installer MSIX Generator — command-line tool for MSIX certificate, manifest, packaging, and EXE installer composition.");

        var certCommand = new Command("cert", "Certificate operations.");
        certCommand.Add(CertGenerateCommand.Create(serviceProvider));
        rootCommand.Add(certCommand);

        var manifestCommand = new Command("manifest", "Manifest config operations.");
        manifestCommand.Add(ManifestCreateCommand.Create(serviceProvider));
        manifestCommand.Add(ManifestShowCommand.Create(serviceProvider));
        manifestCommand.Add(ManifestUpdateCommand.Create(serviceProvider));
        rootCommand.Add(manifestCommand);

        rootCommand.Add(MsixPackCommand.Create(serviceProvider));
        rootCommand.Add(MsixQuickPackCommand.Create(serviceProvider));
        rootCommand.Add(ExePackCommand.Create(serviceProvider));

        return rootCommand;
    }
}