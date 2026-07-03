using InstallerCommons;
using InstallerCommons.ZipHelper;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using System.Reflection;
using System.Text.Json;
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
		var installerComposerConfiguration = CreateInstallerComposerConfiguration();
		var compositionProgress = new ActionProgress<string>(message => DispatcherQueue.TryEnqueue(() => LoadingTextBlock.Text = message));

		// Show the loading UI
		LoadingGrid.Visibility = Visibility.Visible;
		try // Try-finally to hide the loading UI even if an exception is thrown
		{
			await Task.Run(() => InstallerPackageComposer.CreatePackage(installerComposerConfiguration, compositionProgress));

			// Display the success message if it's not automated export
			if (_installerConfigSettingsPath == null) await Content.ShowDialogAsync("Success", "The package has been exported successfully", "OK");
            // Close the window if it's automated export
            else Environment.Exit(0);
		}
		catch (Exception exception)
		{
			// display error message
			await Content.ShowDialogAsync("Error", $"An error occurred while exporting the package\n{exception.Message}: {exception.StackTrace}", "OK");
			if (_installerConfigSettingsPath != null) Environment.Exit(1);
		}
		finally
		{
			LoadingGrid.Visibility = Visibility.Collapsed; // Hide the loading UI
		}
	}

	private async Task<bool> ValidateFieldsAsync()
	{
		try
		{
			InstallerPackageComposer.ValidateConfiguration(CreateInstallerComposerConfiguration());
			return true;
		}
		catch (Exception exception)
		{
            await Content.ShowDialogAsync("Error", exception.Message, "OK");
            return false;
		}
	}

	private InstallerComposerConfiguration CreateInstallerComposerConfiguration()
	{
		var installerComposerConfiguration = new InstallerComposerConfiguration()
		{
			ApplicationId = ApplicationIdTextBox.Text,
			ApplicationName = ApplicationNameTextBox.Text,
			ApplicationPublisher = ApplicationPublisherTextBox.Text,
			ApplicationRootDirectoryPath = ApplicationRootDirectoryPathTextBox.Text,
			ApplicationExecutableFileName = ApplicationExecutableFileNameComboBox.SelectedItem?.ToString(),
			ApplicationInstallationFolderName = ApplicationInstallationFolderNameTextBox.Text,
			ApplicationIconBinary = _applicationIconBinary,
			PackageFilePath = PackageFilePathTextBox.Text
		};

		if (!string.IsNullOrWhiteSpace(ApplicationExecuteAfterInstallTextBox.Text)) installerComposerConfiguration.ExecuteAfterInstall = ApplicationExecuteAfterInstallTextBox.Text;
		if (!string.IsNullOrWhiteSpace(ApplicationExecuteAfterReinstallTextBox.Text)) installerComposerConfiguration.ExecuteAfterReinstall = ApplicationExecuteAfterReinstallTextBox.Text;
		if (!string.IsNullOrWhiteSpace(ApplicationExecuteOnUninstallTextBox.Text)) installerComposerConfiguration.ExecuteOnUninstall = ApplicationExecuteOnUninstallTextBox.Text;

		return installerComposerConfiguration;
	}

	private async void OnAboutMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
	{
		var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
		var aboutString = $"Version: {localVersion}\nAuthor: Howon Lee";
		await Content.ShowDialogAsync("AT Installer Composer", aboutString, "OK");
	}

	private void OnGenerateApplicationIdButtonClicked(object sender, RoutedEventArgs e)
	{
		var applicationId = Guid.NewGuid().ToString(); // Generate a GUID
		ApplicationIdTextBox.Text = applicationId;
	}

	private async void OnBrowseIconFileButtonClicked(object sender, RoutedEventArgs e)
	{
        // Pick a PNG file
        var picker = new FileOpenPicker(AppWindow.Id);
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");

        // Get the file
        var file = await picker.PickSingleFileAsync();
        if (file == null) return; // User cancelled

        // Check if the file is a valid PNG file
        var bytes = File.ReadAllBytes(file.Path);
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
		ApplicationIconThumbnailImage.Source = bitmapImage;
	}

	private void OnNewPackageMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
	{
		// Reset all fields and UI
		ApplicationIdTextBox.Text = "";
		ApplicationNameTextBox.Text = "";
		ApplicationPublisherTextBox.Text = "";
		ApplicationIconThumbnailImage.Source = null;
		ApplicationRootDirectoryPathTextBox.Text = "";
		ApplicationExecutableFileNameComboBox.IsEnabled = false;
		ApplicationExecutableFileNameComboBox.SelectedItem = null;

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
        var picker = new FileOpenPicker(AppWindow.Id);
        picker.FileTypeFilter.Add(".atp");

        // Get the file
        var file = await picker.PickSingleFileAsync();
        if (file == null) return; // User cancelled

        // Show the loading UI
        LoadingGrid.Visibility = Visibility.Visible;
		LoadingTextBlock.Text = "Loading Package Information..."; // Update the loading text

		// Read the manifest file
		string manifestJson = default; // This variable will be set in the task
		await Task.Run(() =>
		{
			manifestJson = ZipFileNative.ReadFileText(file.Path, "manifest.json");
		});

		LoadingGrid.Visibility = Visibility.Collapsed; // Hide the loading UI

		// Deserialize the manifest
		var installManifest = JsonSerializer.Deserialize(manifestJson, SourceGenerationContext.Default.InstallManifest);

		// Setup UI
		ApplicationIdTextBox.Text = installManifest.Id;
		ApplicationNameTextBox.Text = installManifest.Name;
		ApplicationPublisherTextBox.Text = installManifest.Publisher;
		ApplicationInstallationFolderNameTextBox.Text = installManifest.InstallationFolderName ?? installManifest.Name; // Fall back to the application name if the installation folder name is not set (for backward compatibility)

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
		var installerComposerConfiguration = CreateInstallerComposerConfiguration();

        // Pick a file
        var picker = new FileSavePicker(AppWindow.Id);
        picker.FileTypeChoices.Add("AT Installer Composer Config", [".aticconfig"]);
        picker.SuggestedFileName = $"Package.aticconfig";

        // Get the file
		var file = await picker.PickSaveFileAsync();
		if (file == null) return; // User cancelled

        // Write the settings to the file
        InstallerComposerConfigurationFile.SaveConfiguration(installerComposerConfiguration, file.Path);

        // Display the success message
        await Content.ShowDialogAsync("Success", "The settings have been saved successfully", "OK");
	}

	private async void OnLoadSettingsMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
    {
        // Pick a file
		var picker = new FileOpenPicker(AppWindow.Id);
		picker.FileTypeFilter.Add(".aticconfig");

        // Get the file
        var file = await picker.PickSingleFileAsync();
		if (file == null) return; // User cancelled

        // Read the settings file
        LoadSettings(file.Path);

        // Display the success message
        await Content.ShowDialogAsync("Success", "The settings have been loaded successfully", "OK");
    }

    private void LoadSettings(string filePath)
    {
        var installerComposerConfiguration = InstallerComposerConfigurationFile.LoadConfiguration(filePath);

        // Setup UI
        ApplicationIdTextBox.Text = installerComposerConfiguration.ApplicationId;
        ApplicationNameTextBox.Text = installerComposerConfiguration.ApplicationName;
        ApplicationPublisherTextBox.Text = installerComposerConfiguration.ApplicationPublisher;
        ApplicationRootDirectoryPathTextBox.Text = installerComposerConfiguration.ApplicationRootDirectoryPath;
        ApplicationExecutableFileNameComboBox.IsEnabled = true;
        ApplicationExecutableFileNameComboBox.ItemsSource = Directory.GetFiles(installerComposerConfiguration.ApplicationRootDirectoryPath).Select(filePath => new FileInfo(filePath)).Where(file => file.Extension == ".exe").Select(file => file.Name);
        ApplicationExecutableFileNameComboBox.SelectedItem = installerComposerConfiguration.ApplicationExecutableFileName;
        ApplicationInstallationFolderNameTextBox.Text = installerComposerConfiguration.ApplicationInstallationFolderName;
        PackageFilePathTextBox.Text = installerComposerConfiguration.PackageFilePath;

		if (!string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteAfterInstall)) ApplicationExecuteAfterInstallTextBox.Text = installerComposerConfiguration.ExecuteAfterInstall;
		else ApplicationExecuteAfterInstallTextBox.Text = string.Empty;

		if (!string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteAfterReinstall)) ApplicationExecuteAfterReinstallTextBox.Text = installerComposerConfiguration.ExecuteAfterReinstall;
		else ApplicationExecuteAfterReinstallTextBox.Text = string.Empty;

        if (!string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteOnUninstall)) ApplicationExecuteOnUninstallTextBox.Text = installerComposerConfiguration.ExecuteOnUninstall;
		else ApplicationExecuteOnUninstallTextBox.Text = string.Empty;

        // Set the field and thumbnail
        _applicationIconBinary = installerComposerConfiguration.ApplicationIconBinary;
        if (_applicationIconBinary == null) ApplicationIconThumbnailImage.Source = null;
        else ApplyThumbnailFromApplicationIconBinaryField();
    }

    private async void OnBrowseApplicationRootDirectoryRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        // Pick a folder
        var picker = new FolderPicker(AppWindow.Id);

        // Get the folder
        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return; // User cancelled

        // Check if there is any executable file
        var files = Directory.GetFiles(folder.Path).Select(filePath => new FileInfo(filePath));
        var executableFiles = files.Where(file => file.Extension == ".exe").ToList();
        if (executableFiles.Count == 0)
        {
            await Content.ShowDialogAsync("Error", "No executable file found in the selected folder", "OK");
            return;
        }

		// Setup UI
		ApplicationRootDirectoryPathTextBox.Text = folder.Path;
        ApplicationExecutableFileNameComboBox.IsEnabled = true;
        ApplicationExecutableFileNameComboBox.ItemsSource = executableFiles.Select(file => file.Name);
    }

    private async void OnBrowsePackageFilePathCommandRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        // Pick a file
        var dialog = new FileSavePicker(AppWindow.Id);
		dialog.FileTypeChoices.Add("AT Installer Package", [".atp"]);
		dialog.SuggestedFileName = "Package.atp";

        // Get the file
		var file = await dialog.PickSaveFileAsync();
		if (file == null) return; // User cancelled

		// Setup UI
		PackageFilePathTextBox.Text = file.Path;
    }
}
