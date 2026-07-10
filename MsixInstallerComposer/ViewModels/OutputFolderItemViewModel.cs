using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MsixInstallerComposer.ViewModels;

public sealed partial class OutputFolderItemViewModel(ObservableCollection<OutputFolderItemViewModel> parent, string architecture, string path) : ObservableObject
{
    public string Architecture { get; set; } = architecture;
    public string Path { get; set; } = path;

    public string DisplayText => $"[{Architecture}] {Path}";

    [RelayCommand]
    private void DeleteSelf() => parent.Remove(this);
}