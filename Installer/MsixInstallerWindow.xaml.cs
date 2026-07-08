using Installer.Helper;
using Installer.Msix;
using Installer.Msix.Enum;
using Installer.Msix.Model;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Reflection;
using Windows.Management.Deployment;
using WinUIEx;

namespace Installer;

public sealed partial class MsixInstallerWindow : WindowEx
{
    private const int SecondsForExitAfterInstallationComplete = 5;

    private readonly string _packageFilePath;
    private bool _isSilent;
    private bool _shouldAutoInstall;

    private MsixPackageMetadata _packageMetadata;
    private MsixInstalledPackageInfo _installedPackage;

    public MsixInstallerWindow(string packageFilePath, bool isSilent, bool shouldAutoInstall)
    {
        _isSilent = isSilent;
        _shouldAutoInstall = shouldAutoInstall;
        _packageFilePath = packageFilePath;
        InitializeComponent();
        AppWindow.SetIcon("Icon.ico");
        Initialize();
    }

    private async void Initialize()
    {
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };

        AppWindow.SetIcon("icon.ico");

        this.CenterOnScreen();
        this.BringToFront();

        var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version;
        TitleTextBlock.Text += $" {applicationVersion}";

        LoadingGrid.Visibility = Visibility.Visible;

        _packageMetadata = await Task.Run(() => MsixManifestParser.Parse(_packageFilePath));

        ApplicationNameTextBlock.Text = _packageMetadata.DisplayName ?? _packageMetadata.Name ?? "Unknown";
        ApplicationPublisherTextBlock.Text = App.GetLocalizedString("PublisherPrefix") + (_packageMetadata.PublisherDisplayName ?? _packageMetadata.Publisher ?? "Unknown");
        ApplicationVersionTextBlock.Text = App.GetLocalizedString("VersionPrefix") + (_packageMetadata.Version?.ToString() ?? "Unknown");

        if (_packageMetadata.IconBinary != null)
        {
            var bitmapImage = new BitmapImage();
            using var memoryStream = new MemoryStream(_packageMetadata.IconBinary);
            bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
            ApplicationIconImage.Source = bitmapImage;
            ApplicationIconFallbackSymbolIcon.Visibility = Visibility.Collapsed;
        }

        await Task.Delay(250);

        CheckInstalledPackage();

        if (_isSilent || _shouldAutoInstall) OnInstallButtonClicked(null, null);

        LoadingGrid.Visibility = Visibility.Collapsed;
    }

    private void CheckInstalledPackage()
    {
        _installedPackage = MsixDeploymentHelper.FindInstalledPackage(_packageMetadata);
        if (_installedPackage == null) return;

        switch (_installedPackage.Status)
        {
            case MsixInstallStatus.NewerVersion:
                InstallButton.IsEnabled = false;
                InstallButton.Content = App.GetLocalizedString("CantDowngrade");
                if (_isSilent) Environment.Exit(24);
                break;
            case MsixInstallStatus.SameVersion:
                InstallButton.Content = App.GetLocalizedString("Reinstall");
                break;
            case MsixInstallStatus.OlderVersion:
                InstallButton.Content = App.GetLocalizedString("Update");
                break;
        }
    }

    private void RelaunchElevated() => AdministratorHelper.RelaunchElevatedPreservingArguments();

    private async void OnInstallButtonClicked(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        InstallButton.Content = App.GetLocalizedString("Initializing");
        InstallProgressProgressBar.Visibility = Visibility.Visible;
        InstallProgressProgressBar.IsIndeterminate = true;

        if (!AdministratorHelper.IsRunningAsAdministrator())
        {
            RelaunchElevated();
            return;
        }

        int? originalDeveloperModeState = null;
        int silentExitCode = 0;
        bool shouldSilentExit = false;

        try
        {
            InstallButton.Content = App.GetLocalizedString("MsixExtractingCertificate");
            bool certificateInstalled = await Task.Run(() => MsixCertificateHelper.ExtractAndInstallCertificate(_packageFilePath));
            if (!certificateInstalled)
            {
                if (_isSilent) { silentExitCode = 29; shouldSilentExit = true; }
                else
                {
                    InstallProgressTextBlock.Text = App.GetLocalizedString("ErrorPrefix") + App.GetLocalizedString("MsixCertificateExtractionFailed");
                    InstallButton.IsEnabled = true;
                    InstallButton.Content = App.GetLocalizedString("Install");
                }
                return;
            }

            InstallButton.Content = App.GetLocalizedString("MsixEnablingDeveloperMode");
            originalDeveloperModeState = DeveloperModeHelper.BackupDeveloperModeState();
            bool developerModeEnabled = DeveloperModeHelper.TryEnableDeveloperMode();
            if (!developerModeEnabled)
            {
                if (_isSilent) { silentExitCode = 27; shouldSilentExit = true; }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = App.GetLocalizedString("MsixDeveloperModeRequired"),
                        Content = App.GetLocalizedString("MsixDeveloperModeRequiredContent"),
                        PrimaryButtonText = App.GetLocalizedString("MsixContinue"),
                        SecondaryButtonText = App.GetLocalizedString("MsixAbort"),
                        XamlRoot = InstallButton.XamlRoot
                    };
                    var result = await dialog.ShowAsync();
                    if (result != ContentDialogResult.Primary)
                    {
                        InstallButton.IsEnabled = true;
                        InstallButton.Content = App.GetLocalizedString("Install");
                        return;
                    }
                }
                if (shouldSilentExit) return;
            }

            if (_installedPackage is { Status: MsixInstallStatus.SameVersion })
            {
                if (_isSilent) { silentExitCode = 30; shouldSilentExit = true; }
                else
                {
                    var reinstallDialog = new ContentDialog
                    {
                        Title = App.GetLocalizedString("MsixReinstallTitle"),
                        Content = App.GetLocalizedString("MsixReinstallContent"),
                        PrimaryButtonText = App.GetLocalizedString("MsixReinstallYes"),
                        SecondaryButtonText = App.GetLocalizedString("MsixReinstallNo"),
                        XamlRoot = InstallButton.XamlRoot
                    };
                    var reinstallResult = await reinstallDialog.ShowAsync();
                    if (reinstallResult != ContentDialogResult.Primary)
                    {
                        InstallButton.IsEnabled = true;
                        InstallButton.Content = App.GetLocalizedString("Install");
                        return;
                    }
                }
                if (shouldSilentExit) return;

                InstallButton.Content = App.GetLocalizedString("MsixRemovingExistingPackage");
                await MsixDeploymentHelper.RemovePackageAsync(_installedPackage.PackageFullName);
            }

            InstallButton.Content = App.GetLocalizedString("Installing");
            InstallProgressProgressBar.IsIndeterminate = false;
            InstallProgressProgressBar.Value = 0;

            var progress = new Progress<DeploymentProgress>(OnDeploymentProgress);
            var deploymentResult = await MsixDeploymentHelper.AddPackageAsync(_packageFilePath, progress);
            if (!deploymentResult.IsSuccessful)
            {
                if (_isSilent) { silentExitCode = 28; shouldSilentExit = true; }
                else
                {
                    InstallProgressTextBlock.Text = $"{App.GetLocalizedString("ErrorPrefix")}{deploymentResult.ErrorText ?? "0x{deploymentResult.ErrorCode:X8}"}";
                    InstallButton.IsEnabled = true;
                    InstallButton.Content = App.GetLocalizedString("Install");
                }
                return;
            }
        }
        finally
        {
            if (originalDeveloperModeState.HasValue) DeveloperModeHelper.TryRestoreDeveloperModeState(originalDeveloperModeState.Value);
            if (shouldSilentExit) Environment.Exit(silentExitCode);
        }

        InstallProgressProgressBar.Visibility = Visibility.Collapsed;
        InstallProgressTextBlock.Text = App.GetLocalizedString("Completed");

        if (_isSilent) Environment.Exit(0);

        var secondsRemaining = SecondsForExitAfterInstallationComplete;
        InstallButton.Content = string.Format(App.GetLocalizedString("InstallCompleteExitIn"), secondsRemaining);
        var timer = new System.Timers.Timer() { Interval = 1000 };
        timer.Elapsed += (_, __) =>
        {
            secondsRemaining--;
            if (secondsRemaining < 0) Environment.Exit(0);
            DispatcherQueue.TryEnqueue(() => InstallButton.Content = string.Format(App.GetLocalizedString("InstallCompleteExitIn"), secondsRemaining));
        };
        timer.Start();
    }

    private void OnDeploymentProgress(DeploymentProgress progress) => DispatcherQueue.TryEnqueue(() =>
    {
        InstallProgressProgressBar.Value = progress.percentage;
        InstallProgressTextBlock.Text = $"{progress.percentage}%";
    });

    private void OnApplicationNameIsTextTrimmedChanged(Microsoft.UI.Xaml.Controls.TextBlock sender, Microsoft.UI.Xaml.Controls.IsTextTrimmedChangedEventArgs args)
    {
        while (sender.IsTextTrimmed)
        {
            sender.FontSize *= 0.9;
            sender.UpdateLayout();
        }
    }
}