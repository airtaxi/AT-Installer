using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsixInstallerComposer.Helpers;
using MsixInstallerComposer.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MsixInstallerComposer.ViewModels;

public sealed partial class CertificatePageViewModel(LocalizationService localizationService, DialogService dialogService, PickerService pickerService, WinAppService winAppService) : ObservableObject
{
    private const int DefaultValidDays = 1825;
    private const string DefaultPassword = "password";

    [ObservableProperty]
    public partial string Publisher { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ValidDays { get; set; } = DefaultValidDays.ToString();

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    public bool CanGenerate => !string.IsNullOrWhiteSpace(Publisher) && !string.IsNullOrWhiteSpace(ValidDays) && !IsGenerating;

    partial void OnPublisherChanged(string value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnValidDaysChanged(string value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnIsGeneratingChanged(bool value) => OnPropertyChanged(nameof(CanGenerate));

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (!int.TryParse(ValidDays, out var validDays) || validDays <= 0)
        {
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("CertificatePage_GenerateFailedMessageFormat", localizationService.GetLocalizedString("CertificateValidDaysCard.Header")));
            return;
        }

        IsGenerating = true;
        MainWindow.ShowLoading();

        try
        {
            await winAppService.EnsureWinAppBinaryAsync();

            var winAppBinaryPath = winAppService.GetWinAppBinaryPath();
            var tempPfxFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx");

            var arguments = $"cert generate --output \"{tempPfxFilePath}\" --password \"{(string.IsNullOrWhiteSpace(Password) ? DefaultPassword : Password)}\" --valid-days {validDays} --publisher \"{Publisher}\"";

            var exitCode = await Task.Run(() => ProcessHelper.RunCommand(winAppBinaryPath, arguments, Path.GetDirectoryName(winAppBinaryPath)!));

            if (exitCode != 0)
            {
                if (File.Exists(tempPfxFilePath)) File.Delete(tempPfxFilePath);

                MainWindow.HideLoading();
                IsGenerating = false;
                await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("CertificatePage_GenerateFailedMessageFormat", $"winapp.exe exited with code {exitCode}"));
                return;
            }

            var targetFilePath = await pickerService.PickSavePfxFileAsync("certificate");
            if (targetFilePath is null)
            {
                if (File.Exists(tempPfxFilePath)) File.Delete(tempPfxFilePath);

                MainWindow.HideLoading();
                IsGenerating = false;
                return;
            }

            await MoveFileWithRetryAsync(tempPfxFilePath, targetFilePath);

            MainWindow.HideLoading();
            IsGenerating = false;
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetLocalizedString("CertificatePage_GenerateSuccessMessage"));
        }
        catch (Exception exception)
        {
            MainWindow.HideLoading();
            IsGenerating = false;
            await dialogService.ShowMessageAsync(localizationService.GetLocalizedString("AppDisplayName"), localizationService.GetFormattedString("CertificatePage_GenerateFailedMessageFormat", exception.Message));
        }
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