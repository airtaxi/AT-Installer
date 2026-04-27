using System.Text.Json.Serialization;

namespace InstallerCommons;

[JsonSourceGenerationOptions()]
[JsonSerializable(typeof(InstallManifest))]
[JsonSerializable(typeof(UninstallManifest))]
[JsonSerializable(typeof(InstallerComposerConfiguration))]
public partial class SourceGenerationContext : JsonSerializerContext;
