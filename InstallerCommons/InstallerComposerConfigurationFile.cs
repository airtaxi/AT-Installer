using System.Text.Json;

namespace InstallerCommons;

public static class InstallerComposerConfigurationFile
{
    public static InstallerComposerConfiguration LoadConfiguration(string configurationFilePath)
    {
        var configurationJson = File.ReadAllText(configurationFilePath);
        var installerComposerConfiguration = JsonSerializer.Deserialize(configurationJson, SourceGenerationContext.Default.InstallerComposerConfiguration);
        if (installerComposerConfiguration == null) throw new InvalidDataException("The installer composer configuration file is invalid.");
        return installerComposerConfiguration;
    }

    public static void SaveConfiguration(InstallerComposerConfiguration installerComposerConfiguration, string configurationFilePath)
    {
        var configurationJson = JsonSerializer.Serialize(installerComposerConfiguration, SourceGenerationContext.Default.InstallerComposerConfiguration);
        File.WriteAllText(configurationFilePath, configurationJson);
    }
}
