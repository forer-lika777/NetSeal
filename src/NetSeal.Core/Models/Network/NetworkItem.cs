using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Models;

public partial class NetworkItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public Guid Id { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = false;

    public override string ToString()
    {
        return Name + " " + Id;
    }
}

[JsonSerializable(typeof(NetworkItem))]
public partial class NetworkItemJsonContext : JsonSerializerContext
{

}