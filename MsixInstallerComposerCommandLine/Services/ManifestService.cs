using MsixInstallerComposer.Shared.Models;
using System;
using System.IO;
using System.Text.Json;

namespace MsixInstallerComposerCommandLine.Services;

public sealed class ManifestService
{
    private const int ConfigVersion = 1;

    public async Task<string> CreateAsync(string displayName, string applicationDescription, string executableFileName, string logoFilePath, string outputPath, IProgress<string> progress = null)
    {
        byte[] logoFileData = null;
        string logoFileExtension = null;

        if (!string.IsNullOrWhiteSpace(logoFilePath))
        {
            progress?.Report($"Loading logo image: {logoFilePath}");
            logoFileData = await File.ReadAllBytesAsync(logoFilePath);
            logoFileExtension = Path.GetExtension(logoFilePath);
        }

        var config = new AticMsixConfig
        {
            Version = ConfigVersion,
            DisplayName = displayName,
            ApplicationDescription = applicationDescription,
            ExecutableFileName = executableFileName,
            LogoFileExtension = logoFileExtension,
            LogoFileData = logoFileData
        };

        var json = JsonSerializer.Serialize(config, AticMsixConfigSerializerContext.Default.AticMsixConfig);
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.aticmsixconfig");
        await File.WriteAllTextAsync(tempFilePath, json);

        var targetDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

        await MoveFileWithRetryAsync(tempFilePath, outputPath);

        progress?.Report($"Manifest saved to: {outputPath}");

        return outputPath;
    }

    public async Task<AticMsixConfig> LoadAsync(string inputPath)
    {
        var json = await File.ReadAllTextAsync(inputPath);
        var config = JsonSerializer.Deserialize(json, AticMsixConfigSerializerContext.Default.AticMsixConfig);
        if (config is null) throw new InvalidOperationException("Failed to parse manifest config file.");
        return config;
    }

    public async Task<string> UpdateAsync(string inputPath, string displayName, string applicationDescription, string executableFileName, string logoFilePath, bool removeLogo, string outputPath, IProgress<string> progress = null)
    {
        var config = await LoadAsync(inputPath);

        if (displayName is not null) config.DisplayName = displayName;
        if (applicationDescription is not null) config.ApplicationDescription = applicationDescription;
        if (executableFileName is not null) config.ExecutableFileName = executableFileName;

        if (removeLogo)
        {
            config.LogoFileData = null;
            config.LogoFileExtension = null;
        }
        else if (!string.IsNullOrWhiteSpace(logoFilePath))
        {
            progress?.Report($"Loading logo image: {logoFilePath}");
            config.LogoFileData = await File.ReadAllBytesAsync(logoFilePath);
            config.LogoFileExtension = Path.GetExtension(logoFilePath);
        }

        var json = JsonSerializer.Serialize(config, AticMsixConfigSerializerContext.Default.AticMsixConfig);
        var effectiveOutputPath = string.IsNullOrWhiteSpace(outputPath) ? inputPath : outputPath;
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.aticmsixconfig");
        await File.WriteAllTextAsync(tempFilePath, json);

        var targetDirectory = Path.GetDirectoryName(effectiveOutputPath);
        if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

        await MoveFileWithRetryAsync(tempFilePath, effectiveOutputPath);

        progress?.Report($"Manifest updated: {effectiveOutputPath}");

        return effectiveOutputPath;
    }

    private static async Task MoveFileWithRetryAsync(string sourceFilePath, string targetFilePath, int maxRetries = 5)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
                File.Move(sourceFilePath, targetFilePath);
                return;
            }
            catch (IOException)
            {
                if (attempt == maxRetries - 1) throw;
                await Task.Delay(200);
            }
        }
    }
}