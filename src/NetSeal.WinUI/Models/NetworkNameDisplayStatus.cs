using CommunityToolkit.Mvvm.ComponentModel;

namespace NetSeal.WinUI.Models;

public partial class NetworkNameDisplayStatus : ObservableObject
{
    public NetworkNameDisplayStatus(string name)
    {
        Name = name;
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Selected { get; set; } = false;
}
