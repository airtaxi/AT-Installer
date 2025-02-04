using Installer.Helper;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
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
        TbTitle.Text += $" {applicationVersion}";

        // Show loading
        GdLoading.Visibility = Visibility.Visible;

        // Read the manifest file
        string manifestJson = default; // This variable will be set in the task
        await Task.Run(() =>
        {
            manifestJson = ZipFileNative.ReadFileText(_packageFilePath, "manifest.json");
        });
        var installManifest = JsonConvert.DeserializeObject<InstallManifest>(manifestJson);
        _installManifest = installManifest;

        // Set the UI: Text
        TbApplicationName.Text = installManifest.Name;
        TbApplicationPublisher.Text = "Publisher: " + installManifest.Publisher;
        TbApplicationVersion.Text = "Version: " + installManifest.Version.ToString();

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
                BtInstall.Content = "Can't Downgrade";
                if (_isSilent) Environment.Exit(24);
            }
            else if (installedApplicationExecutableFileVersion == installManifest.Version) BtInstall.Content = "Reinstall";
            else BtInstall.Content = "Update";

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
        BtInstall.Content = "Initializing...";
        PbInstallProgress.Visibility = Visibility.Visible;
        PbInstallProgress.IsIndeterminate = true;

        // Create the installation directory
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(_installManifest);

        // Run the uninstall script if it's not the first install
        if (!_isFirstInstall && !string.IsNullOrWhiteSpace(_installManifest.ExecuteOnUninstall))
        {
            BtInstall.Content = "Running Uninstallation Script...";
            try
            {
                await Task.Run(() =>
                {
                    var process = Process.Start(new ProcessStartInfo(Path.Combine(installationDirectoryPath, "uninstall.bat"))
                    {
                        CreateNoWindow = true,
                        WorkingDirectory = installationDirectoryPath
                    });
                    process.Start();
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

        // Check for previous installation
        var existingUninstallManifestPath = Path.Combine(installationDirectoryPath, "uninstall.json");
        UninstallManifest existingUninstallManifest = null;
        if (File.Exists(existingUninstallManifestPath))
        {
            await Task.Run(() =>
            {
                var existingUninstallManifestJson = File.ReadAllText(existingUninstallManifestPath);
                existingUninstallManifest = JsonConvert.DeserializeObject<UninstallManifest>(existingUninstallManifestJson);
            });
        }
        PbInstallProgress.Tag = existingUninstallManifest; // Assign the existing uninstall manifest to the progress bar's tag

        await Task.Run(() =>
        {
            // Update the UI
            DispatcherQueue.TryEnqueue(() => BtInstall.Content = "Preparing Archive...");

            // Extract the archive entry to a temp file
            var tempFile = Path.GetTempFileName();
            ZipFileNative.ExtractFile(_packageFilePath, _installManifest.ArchiveFileName, tempFile, new ActionProgress<ZipProgressStatus>(OnArchiveExtractProgress));

            // Update the UI
            DispatcherQueue.TryEnqueue(() => BtInstall.Content = "Installing...");

            try { ZipFileNative.ExtractToDirectory(tempFile, installationDirectoryPath, new ActionProgress<ZipProgressStatus>(OnArchiveExtractProgress)); }
            catch (Exception exception)
            {
                if (_isSilent)
                {
                    File.Delete(tempFile);
                    Environment.Exit(25);
                }
                DispatcherQueue.TryEnqueue(() => TbInstallProgress.Text = $"ERROR: {exception.Message}");
            }

            // Update the UI
            DispatcherQueue.TryEnqueue(() =>
            {
                TbInstallProgress.Text = "Completed!";
                BtInstall.Content = "Cleaning Up...";
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
        if (!string.IsNullOrWhiteSpace(_installManifest.ExecuteAfterInstall))
        {
            BtInstall.Content = "Running Post Installation Script...";
            try
            {
                await Task.Run(() =>
                {
                    var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/C {_installManifest.ExecuteAfterInstall}")
                    {
                        CreateNoWindow = true,
                        WorkingDirectory = Utils.GetInstallationDirectoryPath(_installManifest)
                    });
                    process.Start();
                    process.WaitForExit();
                });
            }
            catch { } // Ignore
        }

        // Retrieve the new uninstall manifest
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(_installManifest);
        var uninstallManifestPath = Path.Combine(installationDirectoryPath, "uninstall.json");
        var uninstallManifestJson = File.ReadAllText(uninstallManifestPath);
        var uninstallManifest = JsonConvert.DeserializeObject<UninstallManifest>(uninstallManifestJson);

        // Update the UI
        BtInstall.Content = "Registering Program...";

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
                TbInstallProgress.Text = $"ERROR: {exception.Message}";
            }
        });
        if (!isRegistered) return;

        // Update the UI
        PbInstallProgress.Visibility = Visibility.Collapsed;

        if (_isSilent) Environment.Exit(0);

        // Update the UI and setup the timer for exit
        var secondsRemaining = SecondsForExitAfterInstallationComplete;
        BtInstall.Content = $"Install Complete! Exit In {secondsRemaining} Seconds";
        var timer = new System.Timers.Timer() { Interval = 1000 };
        timer.Elapsed += (s, e) =>
        {
            secondsRemaining--;
            if (secondsRemaining < 0) Environment.Exit(0);
            DispatcherQueue.TryEnqueue(() => BtInstall.Content = $"Install Complete! Exit In {secondsRemaining} Seconds");
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
}