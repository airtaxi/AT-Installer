using InstallerCommons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace InstallerComposer.DataTypes
{
    [JsonSourceGenerationOptions()]
    [JsonSerializable(typeof(InstallerComposerConfig))]
    public partial class ComposerSourceGenerationContext : JsonSerializerContext;
}
