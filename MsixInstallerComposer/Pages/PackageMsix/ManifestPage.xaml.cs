using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MsixInstallerComposer.ViewModels;

namespace MsixInstallerComposer.Pages.PackageMsix;

public sealed partial class ManifestPage : Page
{
    public ManifestPageViewModel ViewModel { get; }

    public ManifestPage()
    {
        ViewModel = App.Services.GetRequiredService<ManifestPageViewModel>();

        InitializeComponent();

        DataContext = ViewModel;
    }
}