using Microsoft.Win32;

namespace Installer.Msix;

public static class DeveloperModeHelper
{
    private const string AppModelUnlockRegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock";
    private const string DeveloperModeRegistryValueName = "AllowDevelopmentWithoutDevLicense";

    public static int? BackupDeveloperModeState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AppModelUnlockRegistryKeyPath);
            if (key?.GetValue(DeveloperModeRegistryValueName) is int value) return value;
        }
        catch { }
        return null;
    }

    public static bool TryEnableDeveloperMode()
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(AppModelUnlockRegistryKeyPath, writable: true);
            if (key.GetValue(DeveloperModeRegistryValueName) as int? != 1) key.SetValue(DeveloperModeRegistryValueName, 1, RegistryValueKind.DWord);
            return true;
        }
        catch { return false; }
    }

    public static void TryRestoreDeveloperModeState(int originalState)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AppModelUnlockRegistryKeyPath, writable: true);
            key?.SetValue(DeveloperModeRegistryValueName, originalState, RegistryValueKind.DWord);
        }
        catch { }
    }
}