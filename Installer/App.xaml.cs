using InstallerCommons;
using Newtonsoft.Json;
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

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs launchActivatedEventArgs)
	{
		// Retrieve command line arguments
		var args = Environment.GetCommandLineArgs();

#if DEBUG
		args = ["", "C:\\Data\\Temp\\Package.atp"];
#endif

		// Compose manifest from command line argument
		var packageFilePath = args[1];
		var silent = args.Length > 2 && args[2] == "/silent";

        // Create window with manifest and archive file path
        _window = new InstallerWindow(packageFilePath, silent);
		_window.Activate();
	}

	private WindowEx _window;
}
