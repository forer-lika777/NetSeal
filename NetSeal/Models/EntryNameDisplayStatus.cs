using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Models;

public partial class InterfaceNameDisplayStatus : ObservableObject
{
    public InterfaceNameDisplayStatus(string name)
    {
        Name = name;
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Selected { get; set; } = false;
}
