using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using MsixInstallerComposer.Models;
using MsixInstallerComposer.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MsixInstallerComposer.ViewModels;

public sealed partial class ManifestPageViewModel(LocalizationService localizationService, DialogService dialogService, PickerService pickerService) : ObservableObject
{
    private const int ConfigVersion = 1;
    private const string DefaultFileName = "manifest";
    private const string LogoLoadGlyph = "\uE838";
    private const string LogoRemoveGlyph = "\uE74D";
    private const string AccentButtonStyleKey = "AccentButtonStyle";
    private const string DefaultButtonStyleKey = "DefaultButtonStyle";

    private byte[] _logoFileData;
    private string _logoFileExtension;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApplicationDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExecutableFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasLogoImage { get; set; }

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    public partial string LogoCardHeader { get; set; } = localizationService.GetLocalizedString("ManifestLogoImageCard.Header");

    [ObservableProperty]
    public partial string LogoCardDescription { get; set; } = localizationService.GetLocalizedString("ManifestLogoImageCard.Description");

    [ObservableProperty]
    public partial string LogoButtonGlyph { get; set; } = LogoLoadGlyph;

    [ObservableProperty]
    public partial string LogoButtonText { get; set; } = localizationService.GetLocalizedString("ManifestLogoLoadButtonText.Text");

    [ObservableProperty]
    public partial Style LogoButtonStyle { get; set; } = Application.Current.Resources[AccentButtonStyleKey] as Style;

    public bool CanGenerate => !string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(ApplicationDescription) && !string.IsNullOrWhiteSpace(ExecutableFileName) && !IsGenerating;

    public ICommand LogoButtonCommand => HasLogoImage ? RemoveLogoImageCommand : LoadLogoImageCommand;

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnApplicationDescriptionChanged(string value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnExecutableFileNameChanged(string value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnIsGeneratingChanged(bool value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnHasLogoImageChanged(bool value)
    {
        UpdateLogoCardState();
        OnPropertyChanged(nameof(LogoButtonCommand));
    }

    private void UpdateLogoCardState()
    {
        if (HasLogoImage)
        {
            LogoCardHeader = localizationService.GetLocalizedString("ManifestLogoLoadedCard.Header");
            LogoCardDescription = localizationService.GetLocalizedString("ManifestLogoLoadedCard.Description");
            LogoButtonGlyph = LogoRemoveGlyph;
            LogoButtonText = localizationService.GetLocalizedString("ManifestLogoRemoveButtonText.Text");
            LogoButtonStyle = Application.Current.Resources[DefaultButtonStyleKey] as Style;
        }
        else
        {
            LogoCardHeader = localizationService.GetLocalizedString("ManifestLogoImageCard.Header");
            LogoCardDescription = localizationService.GetLocalizedString("ManifestLogoImageCard.Description");
            LogoButtonGlyph = LogoLoadGlyph;
            LogoButtonText = localizationService.GetLocalizedString("ManifestLogoLoadButtonText.Text");
            LogoButtonStyle = Application.Current.Resources[AccentButtonStyleKey] as Style;
        }
    }

    [RelayCommand]
    private async Task LoadLogoImageAsync()
    {
        var filePath = await pickerService.PickOpenLogoImageFileAsync();
        if (filePath is null) return;

        try
        {
            _logoFileData = await File.ReadAllBytesAsync(filePath);
            _logoFileExtension = Path.GetExtension(filePath);
            HasLogoImage = true;
        }
        catch (Exception exception) { await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("ManifestPage_LogoLoadFailedMessageFormat", exception.Message)); }
    }

    [RelayCommand]
    private void RemoveLogoImage()
    {
        _logoFileData = null;
        _logoFileExtension = null;
        HasLogoImage = false;
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        IsGenerating = true;
        MainWindow.ShowLoading();

        try
        {
            var config = new AticMsixConfig
            {
                Version = ConfigVersion,
                DisplayName = DisplayName,
                ApplicationDescription = ApplicationDescription,
                ExecutableFileName = ExecutableFileName,
                LogoFileExtension = _logoFileExtension,
                LogoFileData = _logoFileData
            };

            var json = JsonSerializer.Serialize(config, AticMsixConfigSerializerContext.Default.AticMsixConfig);
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.aticmsixconfig");
            await File.WriteAllTextAsync(tempFilePath, json);

            var targetFilePath = await pickerService.PickSaveAticMsixConfigFileAsync(DefaultFileName);
            if (targetFilePath is null)
            {
                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);

                MainWindow.HideLoading();
                IsGenerating = false;
                return;
            }

            await MoveFileWithRetryAsync(tempFilePath, targetFilePath);

            MainWindow.HideLoading();
            IsGenerating = false;
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("ManifestPage_GenerateSuccessMessage"));
        }
        catch (Exception exception)
        {
            MainWindow.HideLoading();
            IsGenerating = false;
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("ManifestPage_GenerateFailedMessageFormat", exception.Message));
        }
    }

    [RelayCommand]
    private async Task LoadConfigAsync()
    {
        var filePath = await pickerService.PickOpenAticMsixConfigFileAsync();
        if (filePath is null) return;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize(json, AticMsixConfigSerializerContext.Default.AticMsixConfig);

            if (config is null)
            {
                await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("ManifestPage_LoadFailedMessage"));
                return;
            }

            DisplayName = config.DisplayName ?? string.Empty;
            ApplicationDescription = config.ApplicationDescription ?? string.Empty;
            ExecutableFileName = config.ExecutableFileName ?? string.Empty;
            _logoFileData = config.LogoFileData;
            _logoFileExtension = config.LogoFileExtension;
            HasLogoImage = _logoFileData is not null;

            if (HasLogoImage) await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("ManifestPage_LogoLoadedInfoMessage"));
        }
        catch (Exception exception) { await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("ManifestPage_LoadFailedMessageFormat", exception.Message)); }
    }

    private static async Task MoveFileWithRetryAsync(string sourceFilePath, string targetFilePath, int maxRetries = 5)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
                File.Move(sourceFilePath, targetFilePath);
                return;
            }
            catch (IOException)
            {
                if (attempt == maxRetries - 1) throw;
                await Task.Delay(200);
            }
        }
    }
}