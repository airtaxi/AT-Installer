using MsixInstallerComposer.Models;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.System;

namespace MsixInstallerComposer.Services;

public sealed class StoreUpdateService(ApplicationSettings applicationSettings, ApplicationNotificationService applicationNotificationService) : IDisposable
{
    private const string StoreProductIdentifier = "9P5GS17TCDQX";
    private static readonly TimeSpan s_updateCheckInterval = TimeSpan.FromHours(8);
    private static readonly Uri s_storeProductPageAddress = new($"ms-windows-store://pdp/?ProductId={StoreProductIdentifier}");

    private CancellationTokenSource _updateCheckCancellationTokenSource;
    private bool _isStarted;
    private bool _disposed;

    public string StoreProductIdentifierText => StoreProductIdentifier;

    public void Start()
    {
        if (_isStarted) return;
        applicationSettings.PropertyChanged += OnApplicationSettingsPropertyChanged;
        SynchronizeMonitoringState();
        _isStarted = true;
    }

    public async Task<int> GetAvailableUpdateCountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storeContext = StoreContext.GetDefault();
        var storePackageUpdates = await storeContext.GetAppAndOptionalStorePackageUpdatesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return storePackageUpdates.Count;
    }

    public async Task<bool> OpenStoreProductPageAsync() => await Launcher.LaunchUriAsync(s_storeProductPageAddress);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_isStarted) applicationSettings.PropertyChanged -= OnApplicationSettingsPropertyChanged;
        StopMonitoring();
    }

    private async Task CheckForUpdatesAndNotifyAsync(CancellationToken cancellationToken)
    {
        var availableUpdateCount = await GetAvailableUpdateCountAsync(cancellationToken);
        if (availableUpdateCount > 0) applicationNotificationService.ShowStoreUpdateAvailableNotification(availableUpdateCount, s_storeProductPageAddress);
    }

    private async Task RunPeriodicUpdateCheckLoopAsync(CancellationToken cancellationToken)
    {
        using var periodicTimer = new PeriodicTimer(s_updateCheckInterval);
        try
        {
            while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
            {
                try { await CheckForUpdatesAndNotifyAsync(cancellationToken); }
                catch (COMException) { }
                catch (InvalidOperationException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void StartMonitoring()
    {
        if (_updateCheckCancellationTokenSource is not null) return;

        _updateCheckCancellationTokenSource = new CancellationTokenSource();
        _ = RunPeriodicUpdateCheckLoopAsync(_updateCheckCancellationTokenSource.Token);
    }

    private void StopMonitoring()
    {
        _updateCheckCancellationTokenSource?.Cancel();
        _updateCheckCancellationTokenSource?.Dispose();
        _updateCheckCancellationTokenSource = null;
    }

    private void SynchronizeMonitoringState()
    {
        if (applicationSettings.IsAutomaticUpdateCheckEnabled) StartMonitoring();
        else StopMonitoring();
    }

    private void OnApplicationSettingsPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArguments)
    {
        if (propertyChangedEventArguments.PropertyName != nameof(ApplicationSettings.IsAutomaticUpdateCheckEnabled)) return;
        SynchronizeMonitoringState();
    }
}