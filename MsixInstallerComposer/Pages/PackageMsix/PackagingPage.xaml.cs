using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MsixInstallerComposer.ViewModels;
using System;
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

    private void OnVersionTextBoxTextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        if (sender.Text == "0") return;

        sender.Text = sender.Text.TrimStart('0');
    }
}