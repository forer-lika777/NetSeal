using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace NetSeal.Services.Network;

public class NetworkAdapterService : INetworkAdapterService
{
    public List<NetworkInterface> GetPhysicalEthernetAdapters()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces().ToList();
        var result = new List<NetworkInterface>();
        foreach (var it in interfaces)
        {
            if (IsPhysicalEthernet(it))
            {
                result.Add(it);
            }
        }

        return result;
    }

    public static bool IsPhysicalEthernet(NetworkInterface it)
    {
        if (it.NetworkInterfaceType is not (
            NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.FastEthernetFx or
            NetworkInterfaceType.FastEthernetT))
        {
            return false;
        }

        var mac = it.GetPhysicalAddress();
        if (mac == null || mac.GetAddressBytes().Length != 6)
            return false;

        var text = $"{it.Name} {it.Description}";
        string[] virtualKeywords =
            [
            "Hyper-V", "Virtual", "VMware", "VirtualBox",
            "TAP-", "TUN", "Loopback", "WSL", "Docker",
            "Npcap", "KM-TEST", "Wi-Fi Direct", "Bluetooth"
            ];

        return !virtualKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
