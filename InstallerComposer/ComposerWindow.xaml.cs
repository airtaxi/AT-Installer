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
		var compositionProgress = new ActionProgress<string>(message => DispatcherQueue.TryEnqueue(() => TbLoading.Text = message));

		// Show the loading UI
		GdLoading.Visibility = Visibility.Visible;
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
			GdLoading.Visibility = Visibility.Collapsed; // Hide the loading UI
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
			ApplicationId = TbxApplicationId.Text,
			ApplicationName = TbxApplicationName.Text,
			ApplicationPublisher = TbxApplicationPublisher.Text,
			ApplicationRootDirectoryPath = TbxApplicationRootDirectoryPath.Text,
			ApplicationExecutableFileName = CbxApplicationExecutableFileName.SelectedItem?.ToString(),
			ApplicationInstallationFolderName = TbxApplicationInstallationFolderName.Text,
			ApplicationIconBinary = _applicationIconBinary,
			PackageFilePath = TbxPackageFilePath.Text
		};

		if (!string.IsNullOrWhiteSpace(TbxApplicationExecuteAfterInstall.Text)) installerComposerConfiguration.ExecuteAfterInstall = TbxApplicationExecuteAfterInstall.Text;
		if (!string.IsNullOrWhiteSpace(TbxApplicationExecuteAfterReinstall.Text)) installerComposerConfiguration.ExecuteAfterReinstall = TbxApplicationExecuteAfterReinstall.Text;
		if (!string.IsNullOrWhiteSpace(TbxApplicationExecuteOnUninstall.Text)) installerComposerConfiguration.ExecuteOnUninstall = TbxApplicationExecuteOnUninstall.Text;

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
		TbxApplicationId.Text = applicationId;
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
        var picker = new FileOpenPicker(AppWindow.Id);
        picker.FileTypeFilter.Add(".atp");

        // Get the file
        var file = await picker.PickSingleFileAsync();
        if (file == null) return; // User cancelled

        // Show the loading UI
        GdLoading.Visibility = Visibility.Visible;
		TbLoading.Text = "Loading Package Information..."; // Update the loading text

		// Read the manifest file
		string manifestJson = default; // This variable will be set in the task
		await Task.Run(() =>
		{
			manifestJson = ZipFileNative.ReadFileText(file.Path, "manifest.json");
		});

		GdLoading.Visibility = Visibility.Collapsed; // Hide the loading UI

		// Deserialize the manifest
		var installManifest = JsonSerializer.Deserialize(manifestJson, SourceGenerationContext.Default.InstallManifest);

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
        TbxApplicationId.Text = installerComposerConfiguration.ApplicationId;
        TbxApplicationName.Text = installerComposerConfiguration.ApplicationName;
        TbxApplicationPublisher.Text = installerComposerConfiguration.ApplicationPublisher;
        TbxApplicationRootDirectoryPath.Text = installerComposerConfiguration.ApplicationRootDirectoryPath;
        CbxApplicationExecutableFileName.IsEnabled = true;
        CbxApplicationExecutableFileName.ItemsSource = Directory.GetFiles(installerComposerConfiguration.ApplicationRootDirectoryPath).Select(filePath => new FileInfo(filePath)).Where(file => file.Extension == ".exe").Select(file => file.Name);
        CbxApplicationExecutableFileName.SelectedItem = installerComposerConfiguration.ApplicationExecutableFileName;
        TbxApplicationInstallationFolderName.Text = installerComposerConfiguration.ApplicationInstallationFolderName;
        TbxPackageFilePath.Text = installerComposerConfiguration.PackageFilePath;

		if (!string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteAfterInstall)) TbxApplicationExecuteAfterInstall.Text = installerComposerConfiguration.ExecuteAfterInstall;
		else TbxApplicationExecuteAfterInstall.Text = string.Empty;

		if (!string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteAfterReinstall)) TbxApplicationExecuteAfterReinstall.Text = installerComposerConfiguration.ExecuteAfterReinstall;
		else TbxApplicationExecuteAfterReinstall.Text = string.Empty;

        if (!string.IsNullOrWhiteSpace(installerComposerConfiguration.ExecuteOnUninstall)) TbxApplicationExecuteOnUninstall.Text = installerComposerConfiguration.ExecuteOnUninstall;
		else TbxApplicationExecuteOnUninstall.Text = string.Empty;

        // Set the field and thumbnail
        _applicationIconBinary = installerComposerConfiguration.ApplicationIconBinary;
        if (_applicationIconBinary == null) ImgApplicationIconThumbnail.Source = null;
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
		TbxApplicationRootDirectoryPath.Text = folder.Path;
        CbxApplicationExecutableFileName.IsEnabled = true;
        CbxApplicationExecutableFileName.ItemsSource = executableFiles.Select(file => file.Name);
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
		TbxPackageFilePath.Text = file.Path;
    }
}
