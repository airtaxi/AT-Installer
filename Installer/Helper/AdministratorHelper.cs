using System.Diagnostics;
using System.Security.Principal;

namespace Installer.Helper;

public static class AdministratorHelper
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RelaunchElevatedPreservingArguments()
    {
        var arguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1).Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg));
        if (!arguments.Contains("/install", StringComparison.Ordinal)) arguments += " /install";

        try { Process.Start(new ProcessStartInfo { FileName = Environment.ProcessPath, Arguments = arguments, Verb = "runas", UseShellExecute = true }); }
        catch { }
        Environment.Exit(0);
    }
}