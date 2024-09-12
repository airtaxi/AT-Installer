using System.Diagnostics;

namespace InstallerComposer;

public partial class App : Application
{
	public App()
	{
		DetoursCustomDPI.Handler.OverrideDefaltDpi(192f);
		InitializeComponent();
	}

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		var window = new ComposerWindow(Environment.GetCommandLineArgs().Skip(1).FirstOrDefault());
		window.Activate();
	}
}
