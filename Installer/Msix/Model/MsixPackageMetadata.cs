namespace Installer.Msix.Model;

public sealed class MsixPackageMetadata
{
    public string Name { get; set; }
    public string Publisher { get; set; }
    public Version Version { get; set; }
    public string DisplayName { get; set; }
    public string PublisherDisplayName { get; set; }
    public byte[] IconBinary { get; set; }
}