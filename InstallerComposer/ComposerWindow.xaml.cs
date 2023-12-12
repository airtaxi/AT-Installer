using InstallerCommons;
using Ionic.Zip;
using Ionic.Zlib;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinUI.Utils;
using WinUIEx;

namespace InstallerComposer;

public sealed partial class ComposerWindow : WindowEx
{
	private byte[] _applicationIconBinary;

	public ComposerWindow()
	{
		InitializeComponent();
		ExtendsContentIntoTitleBar = true;
		SystemBackdrop = new MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };
	}

	private async Task ExportPackageAsync()
	{
		var applicationId = TbxApplicationId.Text;
		var applicationName = TbxApplicationName.Text;
		var applicationPublisher = TbxApplicationPublisher.Text;
		var applicationRootDirectoryPath = TbxApplicationRootDirectoryPath.Text;
		var applicationExecutableFileName = CbxApplicationExecutableFileName.SelectedItem?.ToString();
        var applicationInstallationFolderName = TbxApplicationInstallationFolderName.Text;

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
				File.WriteAllText(Path.Combine(instancePath, "manifest.json"), JsonConvert.SerializeObject(installManifest));
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
				uninstallManifestJson = JsonConvert.SerializeObject(uninstallManifest);
			});

			// Archive the application root directory
			TbLoading.Text = "Archiving Application Root Directory..."; // Update the loading text

			await Task.Run(() =>
			{
				using var zip = new ZipFile();
				zip.AlternateEncoding = Encoding.UTF8;
				zip.AlternateEncodingUsage = ZipOption.AsNecessary;
				zip.CompressionLevel = CompressionLevel.None; // No need to compress since this file will be compressed again
				zip.SaveProgress += (s, e) =>
				{
					var progress = (double)e.EntriesSaved / e.EntriesTotal;
					if (double.IsNaN(progress)) return; // Ignore NaN

					DispatcherQueue.TryEnqueue(() =>
					{
						TbLoading.Text = $"Archiving Application Root Directory... ({progress:P0})"; // Update the loading text
					});
				};
				zip.AddDirectory(applicationRootDirectoryPath);
				zip.AddEntry("uninstall.json", uninstallManifestJson);

				var archiveFilePath = Path.Combine(instancePath, "data.bin");
				zip.Save(archiveFilePath);
			});

			// Compress the instance directory
			TbLoading.Text = "Exporting Package..."; // Update the loading text

			await Task.Run(() =>
			{
				var archiveFilePath = Path.Combine(tempDirectoryPath, "Package.atp");
				using var zip = new ZipFile();

				zip.SaveProgress += (s, e) =>
				{
					if (e.CurrentEntry?.FileName != "data.bin") return; // Only show the progress of the data.bin file

					var progress = (double)e.BytesTransferred / e.TotalBytesToTransfer;
					if (double.IsNaN(progress)) return; // Ignore NaN

					DispatcherQueue.TryEnqueue(() =>
					{
						TbLoading.Text = $"Exporting Package... ({progress:P0})"; // Update the loading text
					});
				};
				zip.AddDirectory(instancePath);
				zip.Save(archiveFilePath);
			});

			// Clean up the instance directory
			TbLoading.Text = "Cleaning Up..."; // Update the loading text

			await Task.Run(() =>
			{
				Directory.Delete(instancePath, true);
			});

			TbLoading.Text = "Waiting for User Input..."; // Update the loading text

			// Pick a file
			var picker = new FileSavePicker();
			WinRT.Interop.InitializeWithWindow.Initialize(picker, this.GetWindowHandle());
			picker.FileTypeChoices.Add("AT Package", new List<string>() { ".atp" });
			picker.SuggestedFileName = $"Package.atp";

			// Get the file
			var file = await picker.PickSaveFileAsync();
			if (file == null) // User cancelled
			{
				// Display the cancellation message
				await Content.ShowDialogAsync("Cancelled", "The package has not been exported", "OK");
				return;
			}

			File.Delete(file.Path); // FileSavePicker creates a file with 0 bytes

			// Move the file
			var packageFilePath = Path.Combine(tempDirectoryPath, "Package.atp");
			File.Move(packageFilePath, file.Path);

			// Display the success message
			await Content.ShowDialogAsync("Success", "The package has been exported successfully", "OK");
		}
		catch (Exception)
		{
			// display error message
			await Content.ShowDialogAsync("Error", "An error occurred while exporting the package", "OK");
		}
		finally
		{
			GdLoading.Visibility = Visibility.Collapsed; // Hide the loading UI
		}
	}

	private bool ValidateFields()
	{
		// Retrieve all fields
		var applicationId = TbxApplicationId.Text;
		var applicationName = TbxApplicationName.Text;
		var applicationPublisher = TbxApplicationPublisher.Text;
		var applicationRootDirectoryPath = TbxApplicationRootDirectoryPath.Text;
		var applicationExecutableFileName = CbxApplicationExecutableFileName.SelectedItem?.ToString();
        var applicationInstallationFolderName = TbxApplicationInstallationFolderName.Text;

		// Check if the application ID is a valid GUID
        var isValidApplicationId = Guid.TryParse(applicationId, out _);
		if (!isValidApplicationId)
		{
			TbWarning.Text = "The Application ID field is not a valid GUID";
			TbWarning.Visibility = Visibility.Visible;
			return false;
		}

		// Check if the application name is not empty
		var isValidApplicationName = !string.IsNullOrWhiteSpace(applicationName);
		if (!isValidApplicationName)
		{
			TbWarning.Text = "The Application Name field is empty";
			TbWarning.Visibility = Visibility.Visible;
			return false;
		}

		// Check if the application publisher is not empty
		var isValidApplicationPublisher = !string.IsNullOrWhiteSpace(applicationPublisher);
		if (!isValidApplicationPublisher)
		{
			TbWarning.Text = "The Application Publisher field is empty";
			TbWarning.Visibility = Visibility.Visible;
			return false;
		}


		// Check if the application root directory path is not empty and the directory exists
		var isValidApplicationRootDirectoryPath = !string.IsNullOrWhiteSpace(applicationRootDirectoryPath);
		isValidApplicationRootDirectoryPath &= Directory.Exists(applicationRootDirectoryPath);
		if (!isValidApplicationRootDirectoryPath)
		{
			TbWarning.Text = "The Application Root Directory field is empty or the directory does not exist";
			TbWarning.Visibility = Visibility.Visible;
			return false;
		}

		// Check if the application executable file is selected and the file exists
		var applicationExecutableFilePath = Path.Combine(applicationRootDirectoryPath, applicationExecutableFileName);
		var isValidApplicationExecutableFile = !string.IsNullOrWhiteSpace(applicationExecutableFileName) && File.Exists(applicationExecutableFilePath);
		if (!isValidApplicationExecutableFile)
		{
			TbWarning.Text = "Application Executable File is not selected or the file does not exist";
			TbWarning.Visibility = Visibility.Visible;
			return false;
		}

		// Check if the application installation folder name is not empty
		var isValidApplicationInstallationFolderName = !string.IsNullOrWhiteSpace(applicationInstallationFolderName);
        if (!isValidApplicationInstallationFolderName)
		{
            TbWarning.Text = "The Application Installation Folder Name field is empty";
            TbWarning.Visibility = Visibility.Visible;
            return false;
        }

		// Check if the application installation folder name does not contain illegal characters
		isValidApplicationInstallationFolderName = Utils.RemoveIllegalCharacters(applicationInstallationFolderName) == applicationInstallationFolderName;
        if (!isValidApplicationInstallationFolderName)
		{
            TbWarning.Text = "The Application Installation Folder Name field contains illegal characters";
            TbWarning.Visibility = Visibility.Visible;
            return false;
        }

		return true;
	}

	private void OnGridLayoutUpdated(object sender, object e)
	{
		// Update the window height
		var height = GdMain.ActualHeight + 10; // 10 is the margin by WinUI
		Height = height;
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
		var picker = new FileOpenPicker();
		WinRT.Interop.InitializeWithWindow.Initialize(picker, this.GetWindowHandle());
		picker.ViewMode = PickerViewMode.Thumbnail;
		picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
		picker.FileTypeFilter.Add(".png");

		// Get the file
		var file = await picker.PickSingleFileAsync();
		if (file == null) return; // User cancelled

		// Check if the file is a valid PNG file
		var buffer = await FileIO.ReadBufferAsync(file);
		var isValidPngFile = Utils.IsPng(buffer.ToArray());
		if (!isValidPngFile)
		{
			await Content.ShowDialogAsync("Error", "The selected file is not a valid PNG file", "OK");
			return;
		}

		// Set the binary field
		var bytes = buffer.ToArray();
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

	private async void OnBrowseApplicationRootDirectoryButtonClicked(object sender, RoutedEventArgs e)
	{
		// Pick a folder
		var picker = new FolderPicker();
		WinRT.Interop.InitializeWithWindow.Initialize(picker, this.GetWindowHandle());

		// Get the folder
		var folder = await picker.PickSingleFolderAsync();
		if (folder == null) return; // User cancelled

		// Check if there is any executable file
		var files = await folder.GetFilesAsync();
		var executableFiles = files.Where(file => file.FileType == ".exe");
		if(!executableFiles.Any())
		{
			await Content.ShowDialogAsync("Error", "No executable file found in the selected folder", "OK");
			return;
		}

		// Setup UI
		TbxApplicationRootDirectoryPath.Text = folder.Path;
		CbxApplicationExecutableFileName.IsEnabled = true;
		CbxApplicationExecutableFileName.ItemsSource = executableFiles.Select(file => file.Name);
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
		TbWarning.Visibility = Visibility.Collapsed;

		// Reset the binary field
		_applicationIconBinary = null;
	}

	private void OnExitMenuFlyoutItemClicked(object sender, RoutedEventArgs e) => Environment.Exit(0);

	private async void OnExportPackageMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
	{
		// Validate fields
		var isAllFieldsValid = ValidateFields();
		if (!isAllFieldsValid) return; // Validation failed

		TbWarning.Visibility = Visibility.Collapsed; // Hide the warning message since the validation has succeeded

		await ExportPackageAsync();
	}

	private async void OnLoadPackageInformationMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
	{
		// Pick a file
		var picker = new FileOpenPicker();
		WinRT.Interop.InitializeWithWindow.Initialize(picker, this.GetWindowHandle());
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
			using var zip = ZipFile.Read(file.Path);
			var manifest = zip["manifest.json"];
			using var reader = manifest.OpenReader();
			using var streamReader = new StreamReader(reader);
			manifestJson = streamReader.ReadToEnd();
		});

		GdLoading.Visibility = Visibility.Collapsed; // Hide the loading UI

		// Deserialize the manifest
		var installManifest = JsonConvert.DeserializeObject<InstallManifest>(manifestJson);

		// Setup UI
		TbxApplicationId.Text = installManifest.Id;
		TbxApplicationName.Text = installManifest.Name;
		TbxApplicationPublisher.Text = installManifest.Publisher;
		TbxApplicationInstallationFolderName.Text = installManifest.InstallationFolderName ?? installManifest.Name; // Fall back to the application name if the installation folder name is not set (for backward compatibility)
        TbWarning.Visibility = Visibility.Collapsed;

		// Set the binary field
		_applicationIconBinary = installManifest.IconBinary;

		// Set the thumbnail
		ApplyThumbnailFromApplicationIconBinaryField();
	}
}
