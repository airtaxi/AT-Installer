using MsixInstallerComposerCommandLine.Commands;
using MsixInstallerComposerCommandLine.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;

namespace MsixInstallerComposerCommandLine;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<WinAppBinaryService>();
        services.AddSingleton<CertificateService>();
        services.AddSingleton<ManifestService>();
        services.AddSingleton<MsixPackagingService>();
        services.AddSingleton<ExeComposerService>();
        var serviceProvider = services.BuildServiceProvider();

        var rootCommand = RootCommandBuilder.Build(serviceProvider);

        return await rootCommand.Parse(args).InvokeAsync();
    }
}