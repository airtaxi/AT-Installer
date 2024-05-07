using InstallerCommons;
using Newtonsoft.Json;
using System.Text;
using WinUIEx;

namespace InstallerCommons;

public partial class App : Application
{
    public readonly static byte[] DefaultIconBinary;

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
    }

    protected override void OnLaunched(LaunchActivatedEventArgs launchActivatedEventArgs)
	{
		// Retrieve command line arguments
		var args = Environment.GetCommandLineArgs();

#if DEBUG
		args = ["", "C:\\Data\\Temp\\Package.atp"];
#endif

		// Compose manifest from command line argument
		var packageFilePath = args[1];
		var isSilent = args.Length > 2 && args[2] == "/silent";

        // Create window with manifest and archive file path
        _window = new InstallerWindow(packageFilePath, isSilent);
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
