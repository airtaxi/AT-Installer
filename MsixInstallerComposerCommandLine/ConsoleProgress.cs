using MsixInstallerComposer.Shared.Models;

namespace MsixInstallerComposerCommandLine;

public sealed class ConsoleProgress : IProgress<string>
{
    public void Report(string value) => Console.Error.WriteLine(value);
}

public sealed class ConsoleComposerProgress : IProgress<ComposerProgress>
{
    public void Report(ComposerProgress value) => Console.Error.WriteLine(value.Message);
}

public sealed class ConsoleDownloadProgress : IProgress<DownloadProgress>
{
    private int _lastPercentage = -1;

    public void Report(DownloadProgress value)
    {
        if (value.Percentage == _lastPercentage) return;
        _lastPercentage = value.Percentage;
        Console.Error.Write($"\rDownloading... {value.Percentage}%");
        if (value.Percentage >= 100) Console.Error.WriteLine();
    }
}