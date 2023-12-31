using Installer.Helper;
using InstallerCommons;
using Ionic.Zip;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using WinUIEx;

namespace Installer;

public sealed partial class InstallerWindow : WindowEx
{
    private const int SecondsForExitAfterInstallationComplete = 5;

    private readonly string _packageFilePath;
    private InstallManifest _installManifest;

	public InstallerWindow(string packageFilePath)
	{
        _packageFilePath = packageFilePath;
		InitializeComponent();
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

        await Task.Delay(100); // Wait for the UI to load

        // Read the manifest file
        string manifestJson = default; // This variable will be set in the task
        await Task.Run(() =>
        {
            using var zip = ZipFile.Read(_packageFilePath);
            var manifest = zip["manifest.json"];
            using var reader = manifest.OpenReader();
            using var streamReader = new StreamReader(reader);
            manifestJson = streamReader.ReadToEnd();
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
            }
            else if (installedApplicationExecutableFileVersion == installManifest.Version) BtInstall.Content = "Reinstall";
            else BtInstall.Content = "Update";
        }

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
            // Read the package
            using var package = ZipFile.Read(_packageFilePath);

            // Get the archive entry
            var archiveEntry = package[_installManifest.ArchiveFileName];
            using var archiveEntryReader = archiveEntry.OpenReader();

            // Update the UI
            DispatcherQueue.TryEnqueue(() => BtInstall.Content = "Preparing Archive...");

            // Extract the archive entry to a temp file
            var tempFile = Path.GetTempFileName();
            var tempFileStream = File.OpenWrite(tempFile);
            archiveEntryReader.CopyTo(tempFileStream);
            tempFileStream.Close();
            tempFileStream.Dispose();

            // Read the archive
            using var archive = ZipFile.Read(tempFile);

            // Add the event handler
            archive.ExtractProgress += OnArchiveExtractProgress;

            // Update the UI
            DispatcherQueue.TryEnqueue(() => BtInstall.Content = "Installing...");

            // Extract the archive
            archive.ExtractAll(installationDirectoryPath, ExtractExistingFileAction.OverwriteSilently);

            // Remove the event handler
            archive.ExtractProgress -= OnArchiveExtractProgress;

            // Update the UI
            DispatcherQueue.TryEnqueue(() =>
            {
                BtInstall.Content = "Cleaning Up...";
                PbInstallProgress.IsIndeterminate = true;
            });

            // Delete the temp file
            archive.Dispose(); // Archive should be disposed before deleting to release the file handle
            File.Delete(tempFile);

            // Execute post installation task
            DispatcherQueue.TryEnqueue(OnPostInstallation);
        });
    }

    private async void OnPostInstallation()
    {
        // Retrieve the new uninstall manifest
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(_installManifest);
        var uninstallManifestPath = Path.Combine(installationDirectoryPath, "uninstall.json");
        var uninstallManifestJson = File.ReadAllText(uninstallManifestPath);
        var uninstallManifest = JsonConvert.DeserializeObject<UninstallManifest>(uninstallManifestJson);

        // Update the UI
        BtInstall.Content = "Registering Program...";

        // Register the program to the registry (User)
        await Task.Run(() =>
        {
            ShortcutHelper.CreateShortcutToProgramsFolder(_installManifest);
            RegistryHelper.ComposeUninstallerRegistryKey(uninstallManifest);
        });

        // Update the UI
        PbInstallProgress.Visibility = Visibility.Collapsed;

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

    private void OnArchiveExtractProgress(object sender, ExtractProgressEventArgs e)
    {
        // Overwrite
        e.Cancel = false;

        var progress = (double)e.EntriesExtracted / e.EntriesTotal;
        if (double.IsNaN(progress)) return; // Ignore NaN

        DispatcherQueue.TryEnqueue(() =>
        {
            PbInstallProgress.IsIndeterminate = false;
            PbInstallProgress.Value = progress * 100;
        });
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // Delete the installation directory if it's empty (not installed. Clean up)
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(_installManifest);
        var isEmpty = !Directory.EnumerateDirectories(installationDirectoryPath).Any();
        if (isEmpty) Directory.Delete(installationDirectoryPath);
    }
}