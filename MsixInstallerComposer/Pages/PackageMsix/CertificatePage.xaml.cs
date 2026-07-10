using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MsixInstallerComposer.ViewModels;

namespace MsixInstallerComposer.Pages.PackageMsix;

public sealed partial class CertificatePage : Page
{
    public CertificatePageViewModel ViewModel { get; }

    public CertificatePage()
    {
        ViewModel = App.Services.GetRequiredService<CertificatePageViewModel>();

        InitializeComponent();

        DataContext = ViewModel;
    }
}