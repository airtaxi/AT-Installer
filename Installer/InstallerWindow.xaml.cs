using Installer.Helper;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using WinUIEx;
using InstallerCommons.ZipHelper;
using InstallerCommons;

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
        TitleTextBlock.Text += $" {applicationVersion}";

        // Show loading
        LoadingGrid.Visibility = Visibility.Visible;

        // Read the manifest file
        string manifestJson = default; // This variable will be set in the task
        await Task.Run(() =>
        {
            manifestJson = ZipFileNative.ReadFileText(_packageFilePath, "manifest.json");
        });
        var installManifest = JsonSerializer.Deserialize(manifestJson, SourceGenerationContext.Default.InstallManifest);
        _installManifest = installManifest;

        // Set the UI: Text
        ApplicationNameTextBlock.Text = installManifest.Name;
        ApplicationPublisherTextBlock.Text = App.GetLocalizedString("PublisherPrefix") + installManifest.Publisher;
        ApplicationVersionTextBlock.Text = App.GetLocalizedString("VersionPrefix") + installManifest.Version.ToString();

        if(installManifest.CommitSha != null)
        {
            ApplicationCommitShaTextBlock.Text = App.GetLocalizedString("CommitShaPrefix") + installManifest.CommitSha;
            ApplicationCommitShaTextBlock.Visibility = Visibility.Visible;
        }

        // Set the UI: Icon
        if(installManifest.IconBinary != null)
        {
            var bitmapImage = new BitmapImage();
            using var memoryStream = new MemoryStream(installManifest.IconBinary);
            bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
            ApplicationIconImage.Source = bitmapImage;
            ApplicationIconFallbackSymbolIcon.Visibility = Visibility.Collapsed;
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
                InstallButton.IsEnabled = false;
                InstallButton.Content = App.GetLocalizedString("CantDowngrade");
                if (_isSilent) Environment.Exit(24);
            }
            else if (installedApplicationExecutableFileVersion == installManifest.Version) InstallButton.Content = App.GetLocalizedString("Reinstall");
            else InstallButton.Content = App.GetLocalizedString("Update");

            _isFirstInstall = false;
        }

        if (_isSilent) OnInstallButtonClicked(null, null);

        // Hide loading
        LoadingGrid.Visibility = Visibility.Collapsed;
    }

    private async void OnInstallButtonClicked(object sender, RoutedEventArgs e)
    {
        // Update the UI
        InstallButton.IsEnabled = false;
        InstallButton.Content = App.GetLocalizedString("Initializing");
        InstallProgressProgressBar.Visibility = Visibility.Visible;
        InstallProgressProgressBar.IsIndeterminate = true;

        // Create the installation directory
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(_installManifest);

        // Run the uninstall script if it's not the first install
        if (!_isFirstInstall && !string.IsNullOrWhiteSpace(_installManifest.ExecuteOnUninstall))
        {
            InstallButton.Content = App.GetLocalizedString("RunningUninstallScript");
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
            DispatcherQueue.TryEnqueue(() => InstallButton.Content = App.GetLocalizedString("PreparingArchive"));

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
                        DispatcherQueue.TryEnqueue(() => InstallButton.Content = App.GetLocalizedString("CleaningUpPreviousVersion"));
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
                        DispatcherQueue.TryEnqueue(() => InstallButton.Content = App.GetLocalizedString("CleaningUpPreviousVersion"));
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
            DispatcherQueue.TryEnqueue(() => InstallButton.Content = App.GetLocalizedString("Installing"));

            try { ZipFileNative.ExtractToDirectory(tempFile, installationDirectoryPath, new ActionProgress<ZipProgressStatus>(OnArchiveExtractProgress)); }
            catch (Exception exception)
            {
                if (_isSilent)
                {
                    File.Delete(tempFile);
                    Environment.Exit(25);
                }
                DispatcherQueue.TryEnqueue(() => InstallProgressTextBlock.Text = $"{App.GetLocalizedString("ErrorPrefix")}{exception.Message}");
            }

            // Update the UI
            DispatcherQueue.TryEnqueue(() =>
            {
                InstallProgressTextBlock.Text = App.GetLocalizedString("Completed");
                InstallButton.Content = App.GetLocalizedString("CleaningUp");
                InstallProgressProgressBar.IsIndeterminate = true;
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
                InstallProgressProgressBar.IsIndeterminate = false;
                InstallProgressProgressBar.Value = status.Progress * 100;
                InstallProgressTextBlock.Text = status.FileName;
                _lastExtractionProgressUpdateTime = DateTime.UtcNow;
            }
        });
    }

    private async void OnPostInstallation()
    {
        if (_isFirstInstall && !string.IsNullOrWhiteSpace(_installManifest.ExecuteAfterInstall))
        {
            InstallButton.Content = App.GetLocalizedString("RunningPostInstallScript");
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
            InstallButton.Content = App.GetLocalizedString("RunningPostReinstallScript");
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
        InstallButton.Content = App.GetLocalizedString("RegisteringProgram");

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
                InstallProgressTextBlock.Text = $"{App.GetLocalizedString("ErrorPrefix")}{exception.Message}";
            }
        });
        if (!isRegistered) return;

        // Update the UI
        InstallProgressProgressBar.Visibility = Visibility.Collapsed;

        if (_isSilent) Environment.Exit(0);

        // Update the UI and setup the timer for exit
        var secondsRemaining = SecondsForExitAfterInstallationComplete;
        InstallButton.Content = string.Format(App.GetLocalizedString("InstallCompleteExitIn"), secondsRemaining);
        var timer = new System.Timers.Timer() { Interval = 1000 };
        timer.Elapsed += (s, e) =>
        {
            secondsRemaining--;
            if (secondsRemaining < 0) Environment.Exit(0);
            DispatcherQueue.TryEnqueue(() => InstallButton.Content = string.Format(App.GetLocalizedString("InstallCompleteExitIn"), secondsRemaining));
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