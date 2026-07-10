using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MsixInstallerComposer.Messages;
using MsixInstallerComposer.Pages.PackageMsix;
using MsixInstallerComposer.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace MsixInstallerComposer.Pages;

public sealed partial class PackageMsixPage : Page, IRecipient<AppTitleBarPaneToggledMessage>
{
    public PackageMsixPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        WeakReferenceMessenger.Default.Register(this);
    }

    private void OnPageNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var selectedTag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        var targetType = selectedTag switch
        {
            "Certificate" => typeof(CertificatePage),
            "Manifest" => typeof(ManifestPage),
            "Packaging" => typeof(PackagingPage),
            _ => typeof(CertificatePage)
        };

        if (PageFrame.CurrentSourcePageType != targetType) PageFrame.Navigate(targetType);
    }

    public void Receive(AppTitleBarPaneToggledMessage message) => PageNavigationView.IsPaneOpen = !PageNavigationView.IsPaneOpen;
}