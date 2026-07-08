using Installer.Msix.Model;
using System.IO.Compression;
using System.Xml.Linq;

namespace Installer.Msix;

public static class MsixManifestParser
{
    public static MsixPackageMetadata Parse(string packageFilePath)
    {
        var metadata = new MsixPackageMetadata();
        using var zip = ZipFile.OpenRead(packageFilePath);

        var bundleManifestEntry = zip.GetEntry("AppxMetadata/AppxBundleManifest.xml");
        if (bundleManifestEntry != null)
        {
            ParseBundleManifest(bundleManifestEntry, metadata);
            var firstApplicationPackageFileName = GetFirstApplicationPackageFileName(bundleManifestEntry);
            if (firstApplicationPackageFileName != null)
            {
                var innerEntry = zip.GetEntry(firstApplicationPackageFileName);
                if (innerEntry != null)
                {
                    using var innerZip = new ZipArchive(innerEntry.Open(), ZipArchiveMode.Read);
                    var innerManifestEntry = innerZip.GetEntry("AppxManifest.xml");
                    if (innerManifestEntry != null) ParseAppManifest(innerManifestEntry, metadata);
                    metadata.IconBinary = ExtractLogoFromZip(innerZip);
                }
            }
        }
        else
        {
            var appManifestEntry = zip.GetEntry("AppxManifest.xml");
            if (appManifestEntry != null)
            {
                ParseAppManifest(appManifestEntry, metadata);
                metadata.IconBinary = ExtractLogoFromZip(zip);
            }
        }

        return metadata;
    }

    private static void ParseBundleManifest(ZipArchiveEntry entry, MsixPackageMetadata metadata)
    {
        using var streamReader = new StreamReader(entry.Open());
        var document = XDocument.Parse(streamReader.ReadToEnd());
        var defaultNamespace = document.Root.GetDefaultNamespace();
        var identityElement = document.Root.Element(defaultNamespace + "Identity");
        if (identityElement == null) return;
        metadata.Name = identityElement.Attribute("Name")?.Value;
        metadata.Publisher = identityElement.Attribute("Publisher")?.Value;
        metadata.Version = ParseVersion(identityElement.Attribute("Version")?.Value);
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

    private static void ParseAppManifest(ZipArchiveEntry entry, MsixPackageMetadata metadata)
    {
        using var streamReader = new StreamReader(entry.Open());
        var document = XDocument.Parse(streamReader.ReadToEnd());
        var defaultNamespace = document.Root.GetDefaultNamespace();
        var identityElement = document.Root.Element(defaultNamespace + "Identity");
        if (identityElement != null)
        {
            metadata.Name ??= identityElement.Attribute("Name")?.Value;
            metadata.Publisher ??= identityElement.Attribute("Publisher")?.Value;
            metadata.Version ??= ParseVersion(identityElement.Attribute("Version")?.Value);
        }
        var propertiesElement = document.Root.Element(defaultNamespace + "Properties");
        if (propertiesElement != null)
        {
            metadata.DisplayName ??= propertiesElement.Element(defaultNamespace + "DisplayName")?.Value;
            metadata.PublisherDisplayName ??= propertiesElement.Element(defaultNamespace + "PublisherDisplayName")?.Value;
        }
    }

    private static byte[] ExtractLogoFromZip(ZipArchive zip)
    {
        foreach (var entry in zip.Entries)
        {
            var fullName = entry.FullName;
            if (fullName.StartsWith("Assets/StoreLogo", StringComparison.OrdinalIgnoreCase) && fullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                using var memoryStream = new MemoryStream();
                using (var entryStream = entry.Open()) entryStream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
        return null;
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