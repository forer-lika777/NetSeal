using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Models;

public class NetworkStatus
{
    public bool IsEthernetConnected { get; set; }
    public bool IsPppoeNetworkConnected { get; set; }
    public NetworkConnectivityLevel NetworkConnectivityLevel { get; set; }
    public bool IsInternetAccess { get; set; }
    public bool IsPppoeConnectionManagedByOutside { get; set; }
    public bool IsNetworkConnectionManagedByOutside { get; set; }
    public bool IsNotPppoeNetworkConnection { get; set; }
    public string ActiveNetworkAdapterNames { get; set; } = string.Empty;
    public string ActiveNetworkNames { get; set; } = string.Empty;
    public string ActivePppoeConnectionEntryName { get; set; } = string.Empty;
    public List<NetworkItem> ActiveEthernetNetworks { get; set; } = [];
}
