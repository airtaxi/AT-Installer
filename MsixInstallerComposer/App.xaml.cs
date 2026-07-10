using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MsixInstallerComposer.Models;
using MsixInstallerComposer.Services;
using MsixInstallerComposer.ViewModels;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace MsixInstallerComposer;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; }

    public App()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        InitializeComponent();

        UnhandledException += OnApplicationUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

        Services.GetRequiredService<StoreUpdateService>().Start();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs launchActivatedEventArguments)
    {
        var mainWindow = new MainWindow();
        mainWindow.Activate();
    }

    private static void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ApplicationSettingsService>();
        serviceCollection.AddSingleton(sp => sp.GetRequiredService<ApplicationSettingsService>().Settings);
        serviceCollection.AddSingleton(sp => new LocalizationService(sp.GetRequiredService<ApplicationSettingsService>().Settings.LanguageOverride));
        serviceCollection.AddSingleton(sp => new ApplicationThemeService(sp.GetRequiredService<ApplicationSettingsService>().Settings.Theme));
        serviceCollection.AddSingleton(sp => new ApplicationNotificationService(sp.GetRequiredService<LocalizationService>()));
        serviceCollection.AddSingleton(sp => new StoreUpdateService(sp.GetRequiredService<ApplicationSettings>(), sp.GetRequiredService<ApplicationNotificationService>()));
        serviceCollection.AddSingleton<DialogService>();
        serviceCollection.AddSingleton<PickerService>();
        serviceCollection.AddSingleton<WinAppService>();
        serviceCollection.AddTransient(sp => new SettingsPageViewModel(sp.GetRequiredService<ApplicationSettings>(), sp.GetRequiredService<ApplicationSettingsService>(), sp.GetRequiredService<ApplicationThemeService>(), sp.GetRequiredService<StoreUpdateService>(), sp.GetRequiredService<LocalizationService>(), sp.GetRequiredService<DialogService>()));
        serviceCollection.AddTransient(sp => new PackageExePageViewModel(sp.GetRequiredService<LocalizationService>(), sp.GetRequiredService<DialogService>(), sp.GetRequiredService<PickerService>()));
        serviceCollection.AddTransient(sp => new CertificatePageViewModel(sp.GetRequiredService<LocalizationService>(), sp.GetRequiredService<DialogService>(), sp.GetRequiredService<PickerService>(), sp.GetRequiredService<WinAppService>()));
        serviceCollection.AddTransient(sp => new ManifestPageViewModel(sp.GetRequiredService<LocalizationService>(), sp.GetRequiredService<DialogService>(), sp.GetRequiredService<PickerService>()));
        serviceCollection.AddTransient(sp => new PackagingPageViewModel(sp.GetRequiredService<LocalizationService>(), sp.GetRequiredService<DialogService>(), sp.GetRequiredService<PickerService>(), sp.GetRequiredService<WinAppService>()));
    }

    private static void OnApplicationUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs unhandledExceptionEventArguments)
    {
        WriteException("Microsoft.UI.Xaml.Application.UnhandledException", unhandledExceptionEventArguments.Exception);
        unhandledExceptionEventArguments.Handled = true;
    }

    private static void OnCurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs unhandledExceptionEventArguments)
    {
        if (unhandledExceptionEventArguments.ExceptionObject is Exception exception)
        {
            WriteException("AppDomain.CurrentDomain.UnhandledException", exception);
        }
    }

    private static void OnTaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArguments)
    {
        WriteException("TaskScheduler.UnobservedTaskException", unobservedTaskExceptionEventArguments.Exception);
        unobservedTaskExceptionEventArguments.SetObserved();
    }

    private static void WriteException(string source, Exception exception) => Debug.WriteLine(CreateExceptionMessage(source, exception));

    private static string CreateExceptionMessage(string source, Exception exception)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append('[');
        stringBuilder.Append(source);
        stringBuilder.Append("] ");

        var currentException = exception;
        while (currentException is not null)
        {
            stringBuilder.Append(currentException.GetType().FullName);
            stringBuilder.Append(": ");
            stringBuilder.Append(currentException.Message);
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(currentException.StackTrace);

            currentException = currentException.InnerException;
            if (currentException is not null) stringBuilder.AppendLine("--->");
        }

        return stringBuilder.ToString();
    }
}