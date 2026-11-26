using MsixInstallerComposer.Shared.Enums;
using System;
using System.Collections.Generic;

namespace MsixInstallerComposer.Shared.Models;

public sealed class MsixArchitectureInfo
{
    public required List<MsixArchitecture> Architectures { get; init; }

    public required bool IsBundle { get; init; }

    /// <summary>
    /// Whether the package contains an architecture-neutral application package, which installs on every supported architecture.
    /// </summary>
    public required bool IsNeutral { get; init; }

    public required string PackageName { get; init; }

    public required string PackageDisplayName { get; init; }

    public required string Publisher { get; init; }

    public required string PublisherDisplayName { get; init; }

    public required Version Version { get; init; }
}