using System.Security.Cryptography.X509Certificates;

namespace Installer.Msix;

public static class MsixCertificateHelper
{
    public static bool ExtractAndInstallCertificate(string packageFilePath)
    {
        try
        {
#pragma warning disable SYSLIB0057
            var signerCertificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(packageFilePath));
#pragma warning restore SYSLIB0057

            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            var existing = store.Certificates.Find(X509FindType.FindByThumbprint, signerCertificate.Thumbprint, false);
            if (existing.Count == 0) store.Add(signerCertificate);
            store.Close();
            return true;
        }
        catch { return false; }
    }
}