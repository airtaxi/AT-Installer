using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace InstallerCommons;

[JsonSourceGenerationOptions()]
[JsonSerializable(typeof(InstallManifest))]
[JsonSerializable(typeof(UninstallManifest))]
public partial class SourceGenerationContext : JsonSerializerContext;
