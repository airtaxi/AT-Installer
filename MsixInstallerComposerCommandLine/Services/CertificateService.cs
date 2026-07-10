using MsixInstallerComposer.Shared.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MsixInstallerComposerCommandLine.Services;

public sealed class CertificateService(WinAppBinaryService winAppBinaryService)
{
    private const string DefaultPassword = "password";

    public async Task<string> GenerateAsync(string publisher, int validDays, string password, string outputPath, IProgress<string> progress = null)
    {
        await winAppBinaryService.EnsureWinAppBinaryAsync(progress);

        var winAppBinaryPath = winAppBinaryService.GetWinAppBinaryPath();
        var tempPfxFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx");
        var effectivePassword = string.IsNullOrWhiteSpace(password) ? DefaultPassword : password;

        progress?.Report($"Generating certificate for publisher: {publisher}");

        var arguments = $"cert generate --output {ProcessHelper.EscapeCommandLineArgument(tempPfxFilePath)} --password {ProcessHelper.EscapeCommandLineArgument(effectivePassword)} --valid-days {validDays} --publisher {ProcessHelper.EscapeCommandLineArgument(publisher)}";

        var exitCode = await Task.Run(() => ProcessHelper.RunCommand(winAppBinaryPath, arguments, Path.GetDirectoryName(winAppBinaryPath)!, progress is not null ? progress.Report : null));

        if (exitCode != 0)
        {
            if (File.Exists(tempPfxFilePath)) File.Delete(tempPfxFilePath);
            throw new InvalidOperationException($"winapp.exe exited with code {exitCode}");
        }

        var targetDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

        await MoveFileWithRetryAsync(tempPfxFilePath, outputPath);

        progress?.Report($"Certificate saved to: {outputPath}");

        return outputPath;
    }

    private static async Task MoveFileWithRetryAsync(string sourceFilePath, string targetFilePath, int maxRetries = 5)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
                File.Move(sourceFilePath, targetFilePath);
                return;
            }
            catch (IOException)
            {
                if (attempt == maxRetries - 1) throw;
                await Task.Delay(200);
            }
        }
    }
}