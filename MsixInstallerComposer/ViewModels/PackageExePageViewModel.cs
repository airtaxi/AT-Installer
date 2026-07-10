using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsixInstallerComposer.Shared.Enums;
using MsixInstallerComposer.Shared.Helpers;
using MsixInstallerComposer.Shared.Models;
using MsixInstallerComposer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace MsixInstallerComposer.ViewModels;

public sealed partial class PackageExePageViewModel(LocalizationService localizationService, DialogService dialogService, PickerService pickerService) : ObservableObject
{
    private readonly InstallerComposerService _composerService = new();
    private MsixArchitectureInfo _architectureInfo;
    private string _msixFilePath = string.Empty;

    [ObservableProperty]
    public partial string MsixFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PackageName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PackageDisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PackageVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PackagePublisher { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetectedArchitecturesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasPackageInfo { get; set; }

    [ObservableProperty]
    public partial bool IsX64Available { get; set; }

    [ObservableProperty]
    public partial bool IsX86Available { get; set; }

    [ObservableProperty]
    public partial bool IsArm64Available { get; set; }

    [ObservableProperty]
    public partial bool IsX64Selected { get; set; }

    [ObservableProperty]
    public partial bool IsX86Selected { get; set; }

    [ObservableProperty]
    public partial bool IsArm64Selected { get; set; }

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    public partial string ProgressMessage { get; set; } = string.Empty;

    public ObservableCollection<string> DetectedArchitectures { get; } = [];

    public bool CanGenerate => !string.IsNullOrEmpty(MsixFilePath) && !IsGenerating && (IsX64Selected || IsX86Selected || IsArm64Selected);

    public bool IsMultipleArchitecturesSelected => (IsX64Selected ? 1 : 0) + (IsX86Selected ? 1 : 0) + (IsArm64Selected ? 1 : 0) > 1;

    partial void OnMsixFilePathChanged(string value)
    {
        _msixFilePath = value;
        OnPropertyChanged(nameof(CanGenerate));
    }

    partial void OnIsX64SelectedChanged(bool value) => OnSelectionChanged();

    partial void OnIsX86SelectedChanged(bool value) => OnSelectionChanged();

    partial void OnIsArm64SelectedChanged(bool value) => OnSelectionChanged();

    partial void OnIsGeneratingChanged(bool value) => OnPropertyChanged(nameof(CanGenerate));

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(CanGenerate));
        OnPropertyChanged(nameof(IsMultipleArchitecturesSelected));
    }

    [RelayCommand]
    private async Task OpenMsixFileAsync()
    {
        var filePath = await pickerService.PickOpenMsixFileAsync();
        if (filePath is not null) await LoadMsixFileAsync(filePath);
    }

    public async Task LoadMsixFileAsync(string filePath)
    {
        MsixFilePath = filePath;
        HasPackageInfo = false;

        try
        {
            _architectureInfo = MsixArchitectureDetector.Detect(filePath);

            PackageName = _architectureInfo.PackageName;
            PackageDisplayName = _architectureInfo.PackageDisplayName;
            PackageVersion = _architectureInfo.Version.ToString();
            PackagePublisher = _architectureInfo.Publisher;

            IsX64Available = _architectureInfo.Architectures.Contains(MsixArchitecture.X64);
            IsX86Available = _architectureInfo.Architectures.Contains(MsixArchitecture.X86);
            IsArm64Available = _architectureInfo.Architectures.Contains(MsixArchitecture.Arm64);

            IsX64Selected = IsX64Available;
            IsX86Selected = IsX86Available;
            IsArm64Selected = IsArm64Available;

            DetectedArchitectures.Clear();
            foreach (var architecture in _architectureInfo.Architectures) DetectedArchitectures.Add(FormatArchitectureName(architecture));

            DetectedArchitecturesText = string.Join(", ", DetectedArchitectures);
            HasPackageInfo = true;
        }
        catch (Exception exception)
        {
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackageExePage_LoadFailedMessageFormat", exception.Message));
            ResetPackageInfo();
        }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrEmpty(_msixFilePath)) return;

        var selectedArchitectures = new List<MsixArchitecture>();
        if (IsX64Selected) selectedArchitectures.Add(MsixArchitecture.X64);
        if (IsX86Selected) selectedArchitectures.Add(MsixArchitecture.X86);
        if (IsArm64Selected) selectedArchitectures.Add(MsixArchitecture.Arm64);

        if (selectedArchitectures.Count == 0) return;

        IsGenerating = true;
        MainWindow.ShowLoading();

        try
        {
            var progress = new Progress<ComposerProgress>(ReportProgress);

            var generatedFilePath = await Task.Run(() => _composerService.ComposeAsync(_msixFilePath, selectedArchitectures, progress));

            var isMultiple = selectedArchitectures.Count > 1;
            var suggestedFileName = isMultiple ? "Installers.zip" : $"Installer-{FormatArchitectureName(selectedArchitectures[0])}.exe";
            var extension = isMultiple ? ".zip" : ".exe";

            var targetFilePath = await pickerService.PickSaveFileAsync(suggestedFileName, extension);
            if (targetFilePath is null)
            {
                MainWindow.HideLoading();
                IsGenerating = false;
                return;
            }

            await MoveFileWithRetryAsync(generatedFilePath, targetFilePath);

            MainWindow.HideLoading();
            IsGenerating = false;
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("PackageExePage_GenerateSuccessMessage"));
        }
        catch (Exception exception)
        {
            MainWindow.HideLoading();
            IsGenerating = false;
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("PackageExePage_GenerateFailedMessageFormat", exception.Message));
        }
    }

    private void ReportProgress(ComposerProgress progress) => MainWindow.ShowLoading(progress.Message);

    private void ResetPackageInfo()
    {
        _architectureInfo = null;
        HasPackageInfo = false;
        IsX64Available = false;
        IsX86Available = false;
        IsArm64Available = false;
        IsX64Selected = false;
        IsX86Selected = false;
        IsArm64Selected = false;
        DetectedArchitectures.Clear();
        DetectedArchitecturesText = string.Empty;
    }

    private static string FormatArchitectureName(MsixArchitecture architecture)
    {
        return architecture switch
        {
            MsixArchitecture.X64 => "x64",
            MsixArchitecture.X86 => "x86",
            MsixArchitecture.Arm64 => "ARM64",
            _ => architecture.ToString()
        };
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