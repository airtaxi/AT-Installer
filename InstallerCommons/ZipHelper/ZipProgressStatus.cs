namespace InstallerCommons.ZipHelper;

public class ZipProgressStatus(double progress, string name)
{
    public double Progress { get; init; } = progress;
    public string FileName { get; init; } = name;
}
