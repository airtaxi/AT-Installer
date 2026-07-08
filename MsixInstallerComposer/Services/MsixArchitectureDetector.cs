using MsixInstallerComposer.Enums;
using MsixInstallerComposer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace MsixInstallerComposer.Services;

public static class MsixArchitectureDetector
{
    public static MsixArchitectureInfo Detect(string packageFilePath)
    {
        using var zip = ZipFile.OpenRead(packageFilePath);

        var bundleManifestEntry = zip.GetEntry("AppxMetadata/AppxBundleManifest.xml");
        if (bundleManifestEntry != null) return ParseBundle(bundleManifestEntry);

        var appManifestEntry = zip.GetEntry("AppxManifest.xml");
        if (appManifestEntry != null) return ParseSinglePackage(appManifestEntry);

        throw new InvalidOperationException("MSIX manifest not found in the package.");
    }

    private static MsixArchitectureInfo ParseBundle(ZipArchiveEntry bundleManifestEntry)
    {
        using var streamReader = new StreamReader(bundleManifestEntry.Open());
        var document = XDocument.Parse(streamReader.ReadToEnd());
        var defaultNamespace = document.Root.GetDefaultNamespace();

        var identityElement = document.Root.Element(defaultNamespace + "Identity");
        var packageName = identityElement?.Attribute("Name")?.Value ?? string.Empty;
        var publisher = identityElement?.Attribute("Publisher")?.Value ?? string.Empty;
        var version = ParseVersion(identityElement?.Attribute("Version")?.Value);

        var architectures = new List<MsixArchitecture>();
        var packagesElement = document.Root.Element(defaultNamespace + "Packages");
        if (packagesElement != null)
        {
            foreach (var packageElement in packagesElement.Elements(defaultNamespace + "Package"))
            {
                var typeAttribute = packageElement.Attribute("Type");
                if (typeAttribute != null && typeAttribute.Value != "application") continue;

                var architectureAttribute = packageElement.Attribute("Architecture")?.Value;
                if (architectureAttribute == null) continue;

                var architecture = ParseArchitecture(architectureAttribute);
                if (architecture.HasValue && !architectures.Contains(architecture.Value)) architectures.Add(architecture.Value);
            }
        }

        string packageDisplayName = string.Empty;
        string publisherDisplayName = string.Empty;

        var firstApplicationPackageFileName = GetFirstApplicationPackageFileName(bundleManifestEntry);
        if (firstApplicationPackageFileName != null)
        {
            var innerEntry = ((ZipArchive)bundleManifestEntry.Archive).GetEntry(firstApplicationPackageFileName);
            if (innerEntry != null)
            {
                using var innerZip = new ZipArchive(innerEntry.Open(), ZipArchiveMode.Read);
                var innerManifestEntry = innerZip.GetEntry("AppxManifest.xml");
                if (innerManifestEntry != null) (packageDisplayName, publisherDisplayName) = ParseAppManifestDisplayInfo(innerManifestEntry);
            }
        }

        return new MsixArchitectureInfo
        {
            Architectures = architectures,
            IsBundle = true,
            PackageName = packageName,
            PackageDisplayName = packageDisplayName,
            Publisher = publisher,
            PublisherDisplayName = publisherDisplayName,
            Version = version ?? new Version(0, 0, 0, 0)
        };
    }

    private static MsixArchitectureInfo ParseSinglePackage(ZipArchiveEntry appManifestEntry)
    {
        using var streamReader = new StreamReader(appManifestEntry.Open());
        var document = XDocument.Parse(streamReader.ReadToEnd());
        var defaultNamespace = document.Root.GetDefaultNamespace();

        var identityElement = document.Root.Element(defaultNamespace + "Identity");
        var packageName = identityElement?.Attribute("Name")?.Value ?? string.Empty;
        var publisher = identityElement?.Attribute("Publisher")?.Value ?? string.Empty;
        var version = ParseVersion(identityElement?.Attribute("Version")?.Value);

        var architectures = new List<MsixArchitecture>();
        var processorArchitecture = identityElement?.Attribute("ProcessorArchitecture")?.Value;
        if (processorArchitecture != null)
        {
            var architecture = ParseArchitecture(processorArchitecture);
            if (architecture.HasValue) architectures.Add(architecture.Value);
        }

        var (packageDisplayName, publisherDisplayName) = ParseAppManifestDisplayInfo(appManifestEntry);

        return new MsixArchitectureInfo
        {
            Architectures = architectures,
            IsBundle = false,
            PackageName = packageName,
            PackageDisplayName = packageDisplayName,
            Publisher = publisher,
            PublisherDisplayName = publisherDisplayName,
            Version = version ?? new Version(0, 0, 0, 0)
        };
    }

    private static (string DisplayName, string PublisherDisplayName) ParseAppManifestDisplayInfo(ZipArchiveEntry appManifestEntry)
    {
        using var streamReader = new StreamReader(appManifestEntry.Open());
        var document = XDocument.Parse(streamReader.ReadToEnd());
        var defaultNamespace = document.Root.GetDefaultNamespace();

        var propertiesElement = document.Root.Element(defaultNamespace + "Properties");
        var displayName = propertiesElement?.Element(defaultNamespace + "DisplayName")?.Value ?? string.Empty;
        var publisherDisplayName = propertiesElement?.Element(defaultNamespace + "PublisherDisplayName")?.Value ?? string.Empty;

        return (displayName, publisherDisplayName);
    }

    private static string GetFirstApplicationPackageFileName(ZipArchiveEntry bundleManifestEntry)
    {
        using var streamReader = new StreamReader(bundleManifestEntry.Open());
        var document = XDocument.Parse(streamReader.ReadToEnd());
        var defaultNamespace = document.Root.GetDefaultNamespace();
        var packagesElement = document.Root.Element(defaultNamespace + "Packages");
        if (packagesElement == null) return null;

        foreach (var packageElement in packagesElement.Elements(defaultNamespace + "Package"))
        {
            var typeAttribute = packageElement.Attribute("Type");
            if (typeAttribute != null && typeAttribute.Value == "application") return packageElement.Attribute("FileName")?.Value;
        }

        return null;
    }

    private static MsixArchitecture? ParseArchitecture(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "x64" or "amd64" => MsixArchitecture.X64,
            "x86" or "win32" => MsixArchitecture.X86,
            "arm64" or "aarch64" => MsixArchitecture.Arm64,
            _ => null
        };
    }

    private static Version ParseVersion(string versionString)
    {
        if (string.IsNullOrEmpty(versionString)) return null;

        var parts = versionString.Split('.');
        if (parts.Length != 4) return null;

        if (int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor) && int.TryParse(parts[2], out var build) && int.TryParse(parts[3], out var revision)) return new Version(major, minor, build, revision);

        return null;
    }
}