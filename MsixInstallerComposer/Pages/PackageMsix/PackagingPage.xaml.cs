using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MsixInstallerComposer.ViewModels;
using System.Windows.Input;

namespace MsixInstallerComposer.Pages.PackageMsix;

public sealed partial class PackagingPage : Page
{
    public PackagingPageViewModel ViewModel { get; }

    public PackagingPage()
    {
        ViewModel = App.Services.GetRequiredService<PackagingPageViewModel>();

        InitializeComponent();

        DataContext = ViewModel;
    }
}