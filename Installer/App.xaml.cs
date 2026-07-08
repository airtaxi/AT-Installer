using System.Text;
using Microsoft.Windows.ApplicationModel.Resources;
using WinUIEx;

namespace Installer;

public partial class App : Application
{
    public readonly static byte[] DefaultIconBinary;
    private static ResourceLoader _resourceLoader;

	static App()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Icon.png");
        DefaultIconBinary = File.ReadAllBytes(path);
    }

    private static void OnTaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteErrorLog(e.Exception);
        e.SetObserved();
    }

    private static void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e) => WriteErrorLog((Exception)e.ExceptionObject);

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) => WriteErrorLog(e.Exception);

    private static void WriteErrorLog(Exception exception)
    {
        if (exception.InnerException != null) WriteErrorLog(exception.InnerException);

        var errorLogPath = Path.Combine(AppContext.BaseDirectory, "error.log");

        var errorLog = new StringBuilder();
        errorLog.AppendLine($"[{DateTime.Now}] {exception.Message}");
        errorLog.AppendLine(exception.StackTrace);
        errorLog.AppendLine();

        File.AppendAllText(errorLogPath, errorLog.ToString());
    }

    public App()
    {
        Current.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

        InitializeComponent();
        _resourceLoader = new ResourceLoader();
    }

    public static string GetLocalizedString(string resourceKey) => _resourceLoader.GetString(resourceKey);

    protected override void OnLaunched(LaunchActivatedEventArgs launchActivatedEventArgs)
	{
		// Retrieve command line arguments
		var args = Environment.GetCommandLineArgs();

#if DEBUG
 		args = ["", "E:\\Dev\\Archive\\Archive\\Package.atp"];
#endif

 		// Compose manifest from command line argument
 		var packageFilePath = Path.GetFullPath(args[1]);
 		var isSilent = args.Contains("/silent", StringComparer.OrdinalIgnoreCase);
 		var shouldAutoInstall = args.Contains("/install", StringComparer.OrdinalIgnoreCase);

        // Dispatch to the appropriate window based on package file extension
        var extension = Path.GetExtension(packageFilePath).ToLowerInvariant();
        if (extension == ".msix" || extension == ".msixbundle") _window = new MsixInstallerWindow(packageFilePath, isSilent, shouldAutoInstall);
        else _window = new InstallerWindow(packageFilePath, isSilent, shouldAutoInstall);

        if (isSilent)
        {
            _window.IsShownInSwitchers = false;
            _window.Activate();
            _window.Hide();
        }
        else _window.Activate();
    }

	private WindowEx _window;
}
