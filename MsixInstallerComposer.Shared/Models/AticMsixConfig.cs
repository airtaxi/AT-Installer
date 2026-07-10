using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MsixInstallerComposer.Shared.Models;

public sealed class AticMsixConfig
{
    public int Version { get; set; } = 1;

    public string DisplayName { get; set; } = string.Empty;

    public string ApplicationDescription { get; set; } = string.Empty;

    public string ExecutableFileName { get; set; } = string.Empty;

    public string LogoFileExtension { get; set; }

    public byte[] LogoFileData { get; set; }
}

[JsonSerializable(typeof(AticMsixConfig))]
public sealed partial class AticMsixConfigSerializerContext : JsonSerializerContext;