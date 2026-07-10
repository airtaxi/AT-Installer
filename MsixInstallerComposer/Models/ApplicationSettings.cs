using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace MsixInstallerComposer.Models;

public sealed partial class ApplicationSettings : ObservableObject
{
    [ObservableProperty]
    public partial ElementTheme Theme { get; set; } = ElementTheme.Default;

    [ObservableProperty]
    public partial string LanguageOverride { get; set; } = "";

    [ObservableProperty]
    public partial bool IsAutomaticUpdateCheckEnabled { get; set; } = true;
}