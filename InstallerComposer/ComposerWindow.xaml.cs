using InstallerCommons;
using InstallerCommons.ZipHelper;
using InstallerComposer.DataTypes;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using WindowsAPICodePack.Dialogs;
using WinUIEx;

namespace InstallerComposer;

public sealed partial class ComposerWindow : WindowEx
{
	private byte[] _applicationIconBinary;
	private readonly string _installerConfigSettingsPath;

    public ComposerWindow(string installerConfigSettingsPath)
	{
        InitializeComponent();
        AppWindow.SetIcon("Icon.ico");
        ExtendsContentIntoTitleBar = true;
		SystemBackdrop = new MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };

		if (!string.IsNullOrWhiteSpace(installerConfigSettingsPath))
		{
			_installerConfigSettingsPath = installerConfigSettingsPath;
			ExecuteAutomatedInstall();
        }
    }

    private async void ExecuteAutomatedInstall()
    {
		LoadSettings(_installerConfigSettingsPath);
		var success = await ValidateFieldsAsync();
        if (!success) return;
		await ExportPackageAsync();
    }

    private async Task ExportPackageAsync()
	{
		var applicationId = TbxApplicationId.Text;
		var applicationName = TbxApplicationName.Text;
		var applicationPublisher = TbxApplicationPublisher.Text;
		var applicationRootDirectoryPath = TbxApplicationRootDirectoryPath.Text;
		var applicationExecutableFileName = CbxApplicationExecutableFileName.SelectedItem?.ToString();
        var applicationInstallationFolderName = TbxApplicationInstallationFolderName.Text;
		var packageFilePath = TbxPackageFilePath.Text;

        var applicationExecutableFilePath = Path.Combine(applicationRootDirectoryPath, applicationExecutableFileName);
        var applicationVersionInfo = FileVersionInfo.GetVersionInfo(applicationExecutableFilePath);
        var applicationExecutableFileVersion = new Version(applicationVersionInfo.FileVersion);


        var installManifest = new InstallManifest()
		{
			Id = applicationId,
			Name = applicationName,
			Publisher = applicationPublisher,
			IconBinary = _applicationIconBinary,
			ArchiveFileName = "data.bin",
			ExecutableFileName = applicationExecutableFileName,
			InstallationFolderName = applicationInstallationFolderName,
			Version = applicationExecutableFileVersion
		};

		// Setup the export instance directory
		var exportInstanceId = Guid.NewGuid().ToString();
		var tempDirectoryPath = Path.Combine(Path.GetTempPath(), "ATInstallerComposer");
		var instancePath = Path.Combine(tempDirectoryPath, exportInstanceId);
		Directory.CreateDirectory(instancePath);

		// Show the loading UI
		GdLoading.Visibility = Visibility.Visible;
		try // Try-finally to hide the loading UI even if an exception is thrown
		{
			// Export manifest
			TbLoading.Text = "Exporting Manifest..."; // Update the loading text

			await Task.Run(() =>
			{
				// Write the manifest file
				File.WriteAllText(Path.Combine(instancePath, "manifest.json"), JsonSerializer.Serialize(installManifest));
			});

			// Generate Uninstall Manifest
			TbLoading.Text = "Generating Uninstall Manifest..."; // Update the loading text

			string uninstallManifestJson = default; // This variable will be set in the task
			await Task.Run(() =>
			{
				string[] allFiles = Directory.GetFiles(applicationRootDirectoryPath, "*.*", SearchOption.AllDirectories);
				var uninstallManifest = new UninstallManifest()
				{
					InstallManifest = installManifest,
					InstalledVersion = applicationExecutableFileVersion
				};
				uninstallManifestJson = JsonSerializer.Serialize(uninstallManifest);
			});

			// Archive the application root directory
			TbLoading.Text = "Archiving Application Root Directory..."; // Update the loading text

			await Task.Run(() =>
			{
                // No need to compress since this file will be compressed again
				var archiveFilePath = Path.Combine(instancePath, "data.bin");
				var uninstallManifestFilePath = Path.Combine(applicationRootDirectoryPath, "uninstall.json");
				byte[] existingUninstallManifest = File.Exists(uninstallManifestFilePath) ? File.ReadAllBytes(uninstallManifestFilePath) : null;
				File.WriteAllText(uninstallManifestFilePath, uninstallManifestJson);
                ZipFileNative.CreateFromDirectory(applicationRootDirectoryPath, archiveFilePath, CompressionLevel.NoCompression, new ActionProgress<ZipProgressStatus>((progress) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        TbLoading.Text = $"Archiving Application Root Directory... ({progress.Progress:P0})\n{progress.FileName}"; // Update the loading text
                    });
                }));
				File.Delete(uninstallManifestFilePath);
				if (existingUninstallManifest != null) File.WriteAllBytes(uninstallManifestFilePath, existingUninstallManifest);
			});

			// Compress the instance directory
			TbLoading.Text = "Exporting Package..."; // Update the loading text

			await Task.Run(() =>
			{
				var archiveFilePath = Path.Combine(tempDirectoryPath, "Package.atp");
				ZipFileNative.CreateFromDirectory(instancePath, archiveFilePath, CompressionLevel.Optimal, new ActionProgress<ZipProgressStatus>((progress) =>
				{
					DispatcherQueue.TryEnqueue(() =>
					{
						TbLoading.Text = $"Exporting Package... ({progress.Progress:P0})"; // Update the loading text
					});
				}));

            });

			// Clean up the instance directory
			TbLoading.Text = "Cleaning Up..."; // Update the loading text

			await Task.Run(() =>
			{
				Directory.Delete(instancePath, true);
			});

			TbLoading.Text = "Finishing..."; // Update the loading text

			// Move the file
			var tempPackageFilePath = Path.Combine(tempDirectoryPath, "Package.atp");
			File.Move(tempPackageFilePath, packageFilePath, true);

			// Display the success message if it's not automated export
			if (_installerConfigSettingsPath == null) await Content.ShowDialogAsync("Success", "The package has been exported successfully", "OK");
            // Close the window if it's automated export
            else Process.GetCurrentProcess().Kill();
		}
		catch (Exception exception)
		{
			// display error message
			await Content.ShowDialogAsync("Error", $"An error occurred while exporting the package\n{exception.Message}: {exception.StackTrace}", "OK");
		}
		finally
		{
			GdLoading.Visibility = Visibility.Collapsed; // Hide the loading UI
		}
	}

	private async Task<bool> ValidateFieldsAsync()
	{
		// Retrieve all fields
		var applicationId = TbxApplicationId.Text;
		var applicationName = TbxApplicationName.Text;
		var applicationPublisher = TbxApplicationPublisher.Text;
		var applicationRootDirectoryPath = TbxApplicationRootDirectoryPath.Text;
		var applicationExecutableFileName = CbxApplicationExecutableFileName.SelectedItem?.ToString();
        var applicationInstallationFolderName = TbxApplicationInstallationFolderName.Text;
		var packageFilePath = TbxPackageFilePath.Text;

        // Check if the application ID is a valid GUID
        var isValidApplicationId = Guid.TryParse(applicationId, out _);
		if (!isValidApplicationId)
        {
            await Content.ShowDialogAsync("Error", "The Application ID field is not a valid GUID", "OK");
            return false;
		}

		// Check if the application name is not empty
		var isValidApplicationName = !string.IsNullOrWhiteSpace(applicationName);
		if (!isValidApplicationName)
		{
            await Content.ShowDialogAsync("Error", "The Application Name field is empty", "OK");
			return false;
		}

		// Check if the application publisher is not empty
		var isValidApplicationPublisher = !string.IsNullOrWhiteSpace(applicationPublisher);
		if (!isValidApplicationPublisher)
        {
            await Content.ShowDialogAsync("Error", "The Application Publisher field is empty", "OK");
            return false;
		}


		// Check if the application root directory path is not empty and the directory exists
		var isValidApplicationRootDirectoryPath = !string.IsNullOrWhiteSpace(applicationRootDirectoryPath);
		isValidApplicationRootDirectoryPath &= Directory.Exists(applicationRootDirectoryPath);
		if (!isValidApplicationRootDirectoryPath)
        {
            await Content.ShowDialogAsync("Error", "The Application Root Directory field is empty or the directory does not exist", "OK");
			return false;
		}

		// Check if the application executable file is selected and the file exists
		var applicationExecutableFilePath = Path.Combine(applicationRootDirectoryPath, applicationExecutableFileName);
		var isValidApplicationExecutableFile = !string.IsNullOrWhiteSpace(applicationExecutableFileName) && File.Exists(applicationExecutableFilePath);
		if (!isValidApplicationExecutableFile)
        {
            await Content.ShowDialogAsync("Error", "Application Executable File is not selected or the file does not exist", "OK");
			return false;
		}

		// Check if the application installation folder name is not empty
		var isValidApplicationInstallationFolderName = !string.IsNullOrWhiteSpace(applicationInstallationFolderName);
        if (!isValidApplicationInstallationFolderName)
        {
            await Content.ShowDialogAsync("Error", "The Application Installation Folder Name field is empty", "OK");
            return false;
        }

		// Check if the application installation folder name does not contain illegal characters
		isValidApplicationInstallationFolderName = Utils.RemoveIllegalCharacters(applicationInstallationFolderName) == applicationInstallationFolderName;
        if (!isValidApplicationInstallationFolderName)
        {
            await Content.ShowDialogAsync("Error", "The Application Installation Folder Name field contains illegal characters", "OK");
            return false;
        }

        // Check if Package File Path is set
        var isValidPackageFilePath = !string.IsNullOrWhiteSpace(packageFilePath);
		if (!isValidPackageFilePath)
		{
			await Content.ShowDialogAsync("Error", "The Package File Path field is not set", "OK");
            return false;
        }

        // Check if Package File Path's directory exists
        var isValidPackageFilePathDirectory = Directory.Exists(Path.GetDirectoryName(packageFilePath));
        if (!isValidPackageFilePathDirectory)
		{
            await Content.ShowDialogAsync("Error", "The Package File Path's directory does not exist", "OK");
            return false;
        }

        return true;
	}

	private async void OnAboutMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
	{
		var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
		var aboutString = $"Version: {localVersion}\nAuthor: Howon Lee";
		await Content.ShowDialogAsync("AT Installer Composer", aboutString, "OK");
	}

	private void OnGenerateApplicationIdButtonClicked(object sender, RoutedEventArgs e)
	{
		var id = Guid.NewGuid().ToString(); // Generate a GUID
		TbxApplicationId.Text = id;
	}

	private async void OnBrowseIconFileButtonClicked(object sender, RoutedEventArgs e)
	{
        // Pick a PNG file
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = false;
        dialog.Filters.Add(new CommonFileDialogFilter("PNG Files", "png"));
        dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        dialog.EnsureFileExists = true;


        // Get the file path
        if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return; // User cancelled
        var filePath = dialog.FileName;

		// Check if the file is a valid PNG file
		var bytes = File.ReadAllBytes(filePath);
		var isValidPngFile = Utils.IsPng(bytes);
		if (!isValidPngFile)
		{
			await Content.ShowDialogAsync("Error", "The selected file is not a valid PNG file", "OK");
			return;
		}

		// Set the binary field
		_applicationIconBinary = bytes;

		// Set the thumbnail
		ApplyThumbnailFromApplicationIconBinaryField();
	}

	private void ApplyThumbnailFromApplicationIconBinaryField()
	{
		var bitmapImage = new BitmapImage();
		using var memoryStream = new MemoryStream(_applicationIconBinary);
		bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
		ImgApplicationIconThumbnail.Source = bitmapImage;
	}

	private void OnNewPackageMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
	{
		// Reset all fields and UI
		TbxApplicationId.Text = "";
		TbxApplicationName.Text = "";
		TbxApplicationPublisher.Text = "";
		ImgApplicationIconThumbnail.Source = null;
		TbxApplicationRootDirectoryPath.Text = "";
		CbxApplicationExecutableFileName.IsEnabled = false;
		CbxApplicationExecutableFileName.SelectedItem = null;

		// Reset the binary field
		_applicationIconBinary = null;
	}

	private void OnExitMenuFlyoutItemClicked(object sender, RoutedEventArgs e) => Environment.Exit(0);

	private async void OnExportPackageMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
	{
		// Validate fields
		var isAllFieldsValid = await ValidateFieldsAsync();
		if (!isAllFieldsValid) return; // Validation failed

		await ExportPackageAsync();
	}

	private async void OnImportPackageInformationMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
	{
        // Pick a file
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = false;
        dialog.Filters.Add(new CommonFileDialogFilter("AT Package Files", "atp"));
        dialog.EnsureFileExists = true;

        // Get the file path
        if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return; // User cancelled
		var filePath = dialog.FileName;

        // Show the loading UI
        GdLoading.Visibility = Visibility.Visible;
		TbLoading.Text = "Loading Package Information..."; // Update the loading text

		// Read the manifest file
		string manifestJson = default; // This variable will be set in the task
		await Task.Run(() =>
		{
			manifestJson = ZipFileNative.ReadFileText(filePath, "manifest.json");
		});

		GdLoading.Visibility = Visibility.Collapsed; // Hide the loading UI

		// Deserialize the manifest
		var installManifest = JsonSerializer.Deserialize<InstallManifest>(manifestJson);

		// Setup UI
		TbxApplicationId.Text = installManifest.Id;
		TbxApplicationName.Text = installManifest.Name;
		TbxApplicationPublisher.Text = installManifest.Publisher;
		TbxApplicationInstallationFolderName.Text = installManifest.InstallationFolderName ?? installManifest.Name; // Fall back to the application name if the installation folder name is not set (for backward compatibility)

		// Set the binary field
		_applicationIconBinary = installManifest.IconBinary;

		// Set the thumbnail
		ApplyThumbnailFromApplicationIconBinaryField();
    }

	private async void OnSaveSettingsMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
    {
        // Validate fields
        var isAllFieldsValid = await ValidateFieldsAsync();
        if (!isAllFieldsValid) return; // Validation failed

        // Save the settings
		var settings = new InstallerComposerConfig()
        {
            ApplicationId = TbxApplicationId.Text,
            ApplicationName = TbxApplicationName.Text,
            ApplicationPublisher = TbxApplicationPublisher.Text,
            ApplicationRootDirectoryPath = TbxApplicationRootDirectoryPath.Text,
            ApplicationExecutableFileName = CbxApplicationExecutableFileName.SelectedItem?.ToString(),
            ApplicationInstallationFolderName = TbxApplicationInstallationFolderName.Text,
            ApplicationIconBinary = _applicationIconBinary,
            PackageFilePath = TbxPackageFilePath.Text
        };

        var settingsJson = JsonSerializer.Serialize(settings);

        // Pick a file
        var dialog = new CommonSaveFileDialog();
        dialog.DefaultFileName = "Package.aticconfig";
        dialog.Filters.Add(new CommonFileDialogFilter("AT Installer Composer Config", "aticconfig"));

        // Get the file path
        if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return; // User cancelled
        var filePath = dialog.FileName;

        // Write the settings to the file
        File.WriteAllText(filePath, settingsJson);

        // Display the success message
        await Content.ShowDialogAsync("Success", "The settings have been saved successfully", "OK");
	}

	private async void OnLoadSettingsMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
    {
        // Pick a file
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = false;
        dialog.Filters.Add(new CommonFileDialogFilter("AT Installer Composer Config", "aticconfig"));
        dialog.EnsureFileExists = true;

        // Get the file path
        if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return; // User cancelled
        var filePath = dialog.FileName;

        // Read the settings file
        LoadSettings(filePath);

        // Display the success message
        await Content.ShowDialogAsync("Success", "The settings have been loaded successfully", "OK");
    }

    private void LoadSettings(string filePath)
    {
        var settingsJson = File.ReadAllText(filePath);
        var settings = JsonSerializer.Deserialize<InstallerComposerConfig>(settingsJson);

        // Setup UI
        TbxApplicationId.Text = settings.ApplicationId;
        TbxApplicationName.Text = settings.ApplicationName;
        TbxApplicationPublisher.Text = settings.ApplicationPublisher;
        TbxApplicationRootDirectoryPath.Text = settings.ApplicationRootDirectoryPath;
        CbxApplicationExecutableFileName.IsEnabled = true;
        CbxApplicationExecutableFileName.ItemsSource = Directory.GetFiles(settings.ApplicationRootDirectoryPath).Select(filePath => new FileInfo(filePath)).Where(file => file.Extension == ".exe").Select(file => file.Name);
        CbxApplicationExecutableFileName.SelectedItem = settings.ApplicationExecutableFileName;
        TbxApplicationInstallationFolderName.Text = settings.ApplicationInstallationFolderName;
        TbxPackageFilePath.Text = settings.PackageFilePath;

        // Set the field and thumbnail
        _applicationIconBinary = settings.ApplicationIconBinary;
        ApplyThumbnailFromApplicationIconBinaryField();
    }

    private async void OnBrowseApplicationRootDirectoryRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        // Pick a folder
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        dialog.EnsurePathExists = true;

        // Get the folder path
        if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return; // User cancelled
		var folderPath = dialog.FileName;

        // Check if there is any executable file
        var files = Directory.GetFiles(folderPath).Select(filePath => new FileInfo(filePath));
        var executableFiles = files.Where(file => file.Extension == ".exe").ToList();
        if (executableFiles.Count == 0)
        {
            await Content.ShowDialogAsync("Error", "No executable file found in the selected folder", "OK");
            return;
        }

		// Setup UI
		TbxApplicationRootDirectoryPath.Text = folderPath;
        CbxApplicationExecutableFileName.IsEnabled = true;
        CbxApplicationExecutableFileName.ItemsSource = executableFiles.Select(file => file.Name);
    }

    private void OnBrowsePackageFilePathCommandRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        // Pick a file
        var dialog = new CommonSaveFileDialog();
        dialog.DefaultFileName = "Package.atp";
        dialog.Filters.Add(new CommonFileDialogFilter("AT Package", "atp"));

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return; // User cancelled

        var filePath = dialog.FileName; // Get the file path

		TbxPackageFilePath.Text = filePath;
    }
}
