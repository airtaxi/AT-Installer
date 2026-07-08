namespace Installer.Msix.Model;

public sealed class MsixDeploymentResult
{
    public bool IsSuccessful { get; set; }
    public string ErrorText { get; set; }
    public int ErrorCode { get; set; }
}