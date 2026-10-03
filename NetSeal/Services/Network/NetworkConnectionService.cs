using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace NetSeal.Services.Network;
public static class NetworkConnectionService
{
    public static List<NetworkConnection> GetAllConnections()
    {
        var ethernets = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
                ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                ni.OperationalStatus == OperationalStatus.Up &&
                !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                !ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                !ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase) &&
                !ni.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase) &&
                !ni.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase));

        if (!ethernets.Any())
            return [];

        var result = new List<NetworkConnection>();

        foreach (var ethernet in ethernets)
        {
            if (!Guid.TryParse(ethernet.Id, out var targetAdapterId))
                return [];

            var nlm = (INetworkListManager)new NetworkListManagerClass();

            var enumNets = nlm.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_ALL);
            var netBuf = new IntPtr[1];

            while (enumNets.Next(1, netBuf, out uint netFetched) == 0 && netFetched == 1)
            {
                INetwork? net = null;
                try
                {
                    net = (INetwork)Marshal.GetObjectForIUnknown(netBuf[0]);
                    
                    var conns = net.GetNetworkConnections();
                    var connBuf = new IntPtr[1];

                    while (conns.Next(1, connBuf, out uint connFetched) == 0 && connFetched == 1)
                    {
                        try
                        {
                            var conn = (INetworkConnection)Marshal.GetObjectForIUnknown(connBuf[0]);

                            if (conn.GetAdapterId() == targetAdapterId)
                            {
                                result.Add(new NetworkConnection
                                {
                                    Name = $"({ethernet.Name}) {net.GetName()}",
                                    Id = net.GetNetworkId(),
                                    IsConnected = net.IsConnected,
                                    IsPrivate = net.GetCategory() == NLM_NETWORK_CATEGORY.NLM_NETWORK_CATEGORY_PRIVATE
                                });

                                break;
                            }
                        }
                        finally
                        {
                            Marshal.Release(connBuf[0]);
                        }
                    }
                }
                finally
                {
                    Marshal.Release(netBuf[0]);
                }
            }
        }

        return result;
    }
}