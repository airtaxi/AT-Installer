using System;
using System.Diagnostics;
using System.Text;

namespace MsixInstallerComposer.Shared.Helpers;

public static class ProcessHelper
{
    public static int RunCommand(string fileName, string arguments, string workingDirectoryPath, Action<string> onOutput = null)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectoryPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                onOutput?.Invoke(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                onOutput?.Invoke(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode;
    }

    public static string EscapeCommandLineArgument(string value)
    {
        if (value is null) return string.Empty;

        var builder = new StringBuilder();
        builder.Append('"');

        foreach (var character in value)
        {
            if (character is '"' or '\\') builder.Append('\\');
            builder.Append(character);
        }

        builder.Append('"');
        return builder.ToString();
    }
}