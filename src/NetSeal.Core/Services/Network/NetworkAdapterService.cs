using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using NetSeal.Core.Models;

namespace NetSeal.Core.Services.Network;

public class NetworkAdapterService : INetworkAdapterService
{

    public List<NetworkAdapter> GetNetworkAdapters()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces().ToList();
        List<NetworkAdapter> adapters = [];
        foreach (var its in interfaces)
        {
            adapters.Add(new NetworkAdapter
            {
                Name = its.Name,
                Description = its.Description,
                Id = its.Id,
            });
        }

        return adapters;
    }

    public List<NetworkAdapter> GetPhysicalEthernetAdapters()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => IsPhysicalEthernet(x)).ToList();
        List<NetworkAdapter> adapters = [];
        foreach (var its in interfaces)
        {
            adapters.Add(new NetworkAdapter
            {
                Name = its.Name,
                Description = its.Description,
                Id = its.Id,
            });
        }

        return adapters;
    }

    public List<NetworkAdapter> GetActivePhysicalEthernetAdapters()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && IsPhysicalEthernet(x)).ToList();
        List<NetworkAdapter> adapters = [];
        foreach (var its in interfaces)
        {
            adapters.Add(new NetworkAdapter
            {
                Name = its.Name,
                Description = its.Description,
                Id = its.Id,
            });
        }

        return adapters;
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
