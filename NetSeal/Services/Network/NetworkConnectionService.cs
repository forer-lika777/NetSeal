using NetSeal.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace NetSeal.Services.Network;

public class NetworkConnectionService : INetworkConnectionService
{
    private readonly INetworkAdapterService adapterService;

    public NetworkConnectionService(INetworkAdapterService adapterService)
    {
        this.adapterService = adapterService;
    }

    /// <summary>
    /// 获取系统存储过的所有逻辑网络。
    /// </summary>
    /// <returns></returns>
    public List<INetwork> GetAllNetworks()
    {
        INetworkListManager networkListManager = (INetworkListManager)new NetworkListManagerClass();
        IEnumNetworks networks = networkListManager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED);

        var result = new List<INetwork>();

        nint[] buffer = new IntPtr[1];

        while (networks.Next(1, buffer, out uint netFetched) == 0 && netFetched == 1)
        {
            try
            {
                INetwork? network = (INetwork)Marshal.GetObjectForIUnknown(buffer[0]);
                result.Add(network);
            }
            finally
            {
                Marshal.Release(buffer[0]);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取输入逻辑网络下的所有网络连接。
    /// </summary>
    /// <param name="network">要操作的逻辑网络</param>
    /// <returns></returns>
    public List<INetworkConnection> GetAllNetworkConnections(INetwork network)
    {
        IEnumNetworkConnections connections = network.GetNetworkConnections();

        var result = new List<INetworkConnection>();

        nint[] buffer = new IntPtr[1];

        while (connections.Next(1, buffer, out uint fetched) == 0 && fetched == 1)
        {
            try
            {
                INetworkConnection connetion = (INetworkConnection)Marshal.GetObjectForIUnknown(buffer[0]);
                result.Add(connetion);
            }
            finally
            {
                Marshal.Release(buffer[0]);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取所有接入了以太网的逻辑网络。
    /// </summary>
    /// <returns></returns>
    public List<NetworkConnection> GetAllPhysicalAdapterNetworks()
    {
        var ethernets = adapterService.GetPhysicalEthernetAdapters().Where(x => x.OperationalStatus == OperationalStatus.Up);

        if (!ethernets.Any())
            return [];

        var result = new List<NetworkConnection>();

        foreach (var ethernet in ethernets)
        {
            if (!Guid.TryParse(ethernet.Id, out var targetAdapterId))
                continue;

            var networks = GetAllNetworks();
            foreach (var network in networks)
            {
                var connections = GetAllNetworkConnections(network);
                foreach (var connection in connections)
                {
                    if (connection.GetAdapterId() == targetAdapterId)
                    {
                        result.Add(new NetworkConnection
                        {
                            Name = $"({ethernet.Name}) {network.GetName()}",
                            Id = network.GetNetworkId(),
                            IsConnected = network.IsConnected,
                            IsPrivate = network.GetCategory() == NLM_NETWORK_CATEGORY.NLM_NETWORK_CATEGORY_PRIVATE
                        });

                        break;
                    }
                }
            }
        }

        return result;
    }
}