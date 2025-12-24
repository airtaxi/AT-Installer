using Installer.Helper;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using WinUIEx;
using InstallerCommons.ZipHelper;
using InstallerCommons;
using WinUI3Localizer;

namespace Installer;

public sealed partial class InstallerWindow : WindowEx
{
    private const int SecondsForExitAfterInstallationComplete = 5;

    private readonly string _packageFilePath;
    private InstallManifest _installManifest;
    private bool _isFirstInstall = true;
    private bool _isSilent;

    public InstallerWindow(string packageFilePath, bool isSlient)
    {
        _isSilent = isSlient;
        _packageFilePath = packageFilePath;
		InitializeComponent();
        AppWindow.SetIcon("Icon.ico");
        Initialize();
	}

    private async void Initialize()
    {
        // Set the window properties
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };

        // Set the window icon
        AppWindow.SetIcon("icon.ico");

        // Center the window and bring it to the front
        this.CenterOnScreen();
        this.BringToFront();

        // Set the title
        var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version;
        TbTitle.Text += $" {applicationVersion}";

        // Show loading
        GdLoading.Visibility = Visibility.Visible;

        // Read the manifest file
        string manifestJson = default; // This variable will be set in the task
        await Task.Run(() =>
        {
            manifestJson = ZipFileNative.ReadFileText(_packageFilePath, "manifest.json");
        });
        var installManifest = JsonSerializer.Deserialize(manifestJson, SourceGenerationContext.Default.InstallManifest);
        _installManifest = installManifest;

        // Set the UI: Text
        TbApplicationName.Text = installManifest.Name;
        TbApplicationPublisher.Text = Localizer.Get().GetLocalizedString("PublisherPrefix") + installManifest.Publisher;
        TbApplicationVersion.Text = Localizer.Get().GetLocalizedString("VersionPrefix") + installManifest.Version.ToString();

        if(installManifest.CommitSha != null)
        {
            TbApplicationCommitSha.Text = Localizer.Get().GetLocalizedString("CommitShaPrefix") + installManifest.CommitSha;
            TbApplicationCommitSha.Visibility = Visibility.Visible;
        }

        // Set the UI: Icon
        if(installManifest.IconBinary != null)
        {
            var bitmapImage = new BitmapImage();
            using var memoryStream = new MemoryStream(installManifest.IconBinary);
            bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
            ImgApplicationIcon.Source = bitmapImage;
            SiApplicationIconFallback.Visibility = Visibility.Collapsed;
        }

        // Fake loading
        await Task.Delay(250);

        // Set the UI: Install button
        var installedApplicationInstallationDirectoryPath = Utils.GetInstallationDirectoryPath(installManifest);
        var installedApplicationExecutablePath = Path.Combine(installedApplicationInstallationDirectoryPath, installManifest.ExecutableFileName);
        if (File.Exists(installedApplicationExecutablePath)) // Application is installed
        {
            var installedApplicationVersionInfo = FileVersionInfo.GetVersionInfo(installedApplicationExecutablePath);
            var installedApplicationExecutableFileVersion = new Version(installedApplicationVersionInfo.FileVersion);
            if (installedApplicationExecutableFileVersion > installManifest.Version)
            {
                BtInstall.IsEnabled = false;
                BtInstall.Content = Localizer.Get().GetLocalizedString("CantDowngrade");
                if (_isSilent) Environment.Exit(24);
            }
            else if (installedApplicationExecutableFileVersion == installManifest.Version) BtInstall.Content = Localizer.Get().GetLocalizedString("Reinstall");
            else BtInstall.Content = Localizer.Get().GetLocalizedString("Update");

            _isFirstInstall = false;
        }

        if (_isSilent) OnInstallButtonClicked(null, null);

        // Hide loading
        GdLoading.Visibility = Visibility.Collapsed;
    }

    private async void OnInstallButtonClicked(object sender, RoutedEventArgs e)
    {
        // Update the UI
        BtInstall.IsEnabled = false;
        BtInstall.Content = Localizer.Get().GetLocalizedString("Initializing");
        PbInstallProgress.Visibility = Visibility.Visible;
        PbInstallProgress.IsIndeterminate = true;

        // Create the installation directory
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(_installManifest);

        // Run the uninstall script if it's not the first install
        if (!_isFirstInstall && !string.IsNullOrWhiteSpace(_installManifest.ExecuteOnUninstall))
        {
            BtInstall.Content = Localizer.Get().GetLocalizedString("RunningUninstallScript");
            try
            {
                await Task.Run(() =>
                {
                    var process = Process.Start(new ProcessStartInfo(Path.Combine(installationDirectoryPath, "uninstall.bat"))
                    {
                        CreateNoWindow = true,
                        WorkingDirectory = installationDirectoryPath
                    });
                    process.WaitForExit();
                });
            }
            catch { } // Ignore
        }

        // Try close existing process
        try
        {
            var existingProcess = Process.GetProcessesByName(_installManifest.ExecutableFileName.Replace(".exe", ""));
            foreach (var process in existingProcess) process.Kill(true);
        }
        catch { } // Ignore

        await Task.Run(() =>
        {
            // Update the UI
            DispatcherQueue.TryEnqueue(() => BtInstall.Content = Localizer.Get().GetLocalizedString("PreparingArchive"));

            // Extract the archive entry to a temp file
            var tempFile = Path.GetTempFileName();
            ZipFileNative.ExtractFile(_packageFilePath, _installManifest.ArchiveFileName, tempFile, new ActionProgress<ZipProgressStatus>(OnArchiveExtractProgress));

            // Check for previous installation and clean up
            try
            {
                UninstallManifest uninstallManifest = null;
                var existingUninstallManifestPath = Path.Combine(installationDirectoryPath, "uninstall.json");

                // 1. Try to read from existing installation
                if (File.Exists(existingUninstallManifestPath))
                {
                    try
                    {
                        var json = File.ReadAllText(existingUninstallManifestPath);
                        uninstallManifest = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.UninstallManifest);
                    }
                    catch { }
                }

                // 2. If not found or failed, try to read from the new package
                if (uninstallManifest == null)
                {
                    var json = ZipFileNative.ReadFileText(tempFile, "uninstall.json");
                    if (!string.IsNullOrEmpty(json))
                    {
                        uninstallManifest = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.UninstallManifest);
                    }
                }

                if (uninstallManifest != null)
                {
                    if (uninstallManifest.InstalledFiles != null)
                    {
                        DispatcherQueue.TryEnqueue(() => BtInstall.Content = Localizer.Get().GetLocalizedString("CleaningUpPreviousVersion"));
                        foreach (var file in uninstallManifest.InstalledFiles)
                        {
                            var filePath = Path.Combine(installationDirectoryPath, file);
                            if (File.Exists(filePath))
                            {
                                try { File.Delete(filePath); } catch { }
                            }
                        }

                        // Clean up empty directories
                        foreach (var file in uninstallManifest.InstalledFiles)
                        {
                            try
                            {
                                var directoryPath = Path.GetDirectoryName(Path.Combine(installationDirectoryPath, file));
                                if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
                                {
                                    Directory.Delete(directoryPath);
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        // Fallback: Delete the entire installation directory
                        DispatcherQueue.TryEnqueue(() => BtInstall.Content = Localizer.Get().GetLocalizedString("CleaningUpPreviousVersion"));
                        try
                        {
                            if (Directory.Exists(installationDirectoryPath)) Directory.Delete(installationDirectoryPath, true);
                        }
                        catch { }
                    }
                }
            }
            catch { } // Ignore errors during cleanup

            // Update the UI
            DispatcherQueue.TryEnqueue(() => BtInstall.Content = Localizer.Get().GetLocalizedString("Installing"));

            try { ZipFileNative.ExtractToDirectory(tempFile, installationDirectoryPath, new ActionProgress<ZipProgressStatus>(OnArchiveExtractProgress)); }
            catch (Exception exception)
            {
                if (_isSilent)
                {
                    File.Delete(tempFile);
                    Environment.Exit(25);
                }
                DispatcherQueue.TryEnqueue(() => TbInstallProgress.Text = $"{Localizer.Get().GetLocalizedString("ErrorPrefix")}{exception.Message}");
            }

            // Update the UI
            DispatcherQueue.TryEnqueue(() =>
            {
                TbInstallProgress.Text = Localizer.Get().GetLocalizedString("Completed");
                BtInstall.Content = Localizer.Get().GetLocalizedString("CleaningUp");
                PbInstallProgress.IsIndeterminate = true;
            });

            // Delete the temp file
            File.Delete(tempFile);

            // Execute post installation task
            DispatcherQueue.TryEnqueue(OnPostInstallation);
        });
    }

    private DateTime _lastExtractionProgressUpdateTime = DateTime.MinValue;
    private void OnArchiveExtractProgress(ZipProgressStatus status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (DateTime.UtcNow > _lastExtractionProgressUpdateTime.AddSeconds(0.13) || status.Progress == 1)
            {
                PbInstallProgress.IsIndeterminate = false;
                PbInstallProgress.Value = status.Progress * 100;
                TbInstallProgress.Text = status.FileName;
                _lastExtractionProgressUpdateTime = DateTime.UtcNow;
            }
        });
    }

    private async void OnPostInstallation()
    {
        if (_isFirstInstall && !string.IsNullOrWhiteSpace(_installManifest.ExecuteAfterInstall))
        {
            BtInstall.Content = Localizer.Get().GetLocalizedString("RunningPostInstallScript");
            try
            {
                await Task.Run(() =>
                {
                    var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/C {_installManifest.ExecuteAfterInstall}")
                    {
                        CreateNoWindow = true,
                        WorkingDirectory = Utils.GetInstallationDirectoryPath(_installManifest)
                    });
                    process.WaitForExit();
                });
            }
            catch { } // Ignore
        }
        else if (!_isFirstInstall && !string.IsNullOrWhiteSpace(_installManifest.ExecuteAfterReinstall))
        {
            BtInstall.Content = Localizer.Get().GetLocalizedString("RunningPostReinstallScript");
            try
            {
                await Task.Run(() =>
                {
                    var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/C {_installManifest.ExecuteAfterReinstall}")
                    {
                        CreateNoWindow = true,
                        WorkingDirectory = Utils.GetInstallationDirectoryPath(_installManifest)
                    });
                    process.WaitForExit();
                });
            }
            catch { } // Ignore
        }

        // Retrieve the new uninstall manifest
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(_installManifest);
        var uninstallManifestPath = Path.Combine(installationDirectoryPath, "uninstall.json");
        var uninstallManifestJson = File.ReadAllText(uninstallManifestPath);
        var uninstallManifest = JsonSerializer.Deserialize(uninstallManifestJson, SourceGenerationContext.Default.UninstallManifest);

        // Update the UI
        BtInstall.Content = Localizer.Get().GetLocalizedString("RegisteringProgram");

        // Register the program to the registry (User)
        bool isRegistered = false;
        await Task.Run(() =>
        {
            try
            {
                ShortcutHelper.CreateShortcutToProgramsFolder(_installManifest);
                RegistryHelper.ComposeUninstallerRegistryKey(uninstallManifest);
                isRegistered = true;
            }
            catch (Exception exception)
            {
                if (_isSilent) Environment.Exit(26);
                TbInstallProgress.Text = $"{Localizer.Get().GetLocalizedString("ErrorPrefix")}{exception.Message}";
            }
        });
        if (!isRegistered) return;

        // Update the UI
        PbInstallProgress.Visibility = Visibility.Collapsed;

        if (_isSilent) Environment.Exit(0);

        // Update the UI and setup the timer for exit
        var secondsRemaining = SecondsForExitAfterInstallationComplete;
        BtInstall.Content = string.Format(Localizer.Get().GetLocalizedString("InstallCompleteExitIn"), secondsRemaining);
        var timer = new System.Timers.Timer() { Interval = 1000 };
        timer.Elapsed += (s, e) =>
        {
            secondsRemaining--;
            if (secondsRemaining < 0) Environment.Exit(0);
            DispatcherQueue.TryEnqueue(() => BtInstall.Content = string.Format(Localizer.Get().GetLocalizedString("InstallCompleteExitIn"), secondsRemaining));
        };
        timer.Start();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // Delete the installation directory if it's empty (not installed. Clean up)
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(_installManifest);
        var isEmpty = !Directory.EnumerateDirectories(installationDirectoryPath).Any();
        if (isEmpty) Directory.Delete(installationDirectoryPath);
    }

    private void OnApplicationNameIsTextTrimmedChanged(Microsoft.UI.Xaml.Controls.TextBlock sender, Microsoft.UI.Xaml.Controls.IsTextTrimmedChangedEventArgs args)
    {
        while (sender.IsTextTrimmed)
        {
            sender.FontSize *= 0.9;
            sender.UpdateLayout();
        }
    }
}