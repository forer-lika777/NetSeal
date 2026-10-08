using NetSeal.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace NetSeal.Services.Network;

public class NetworkService : INetworkService
{
    private readonly INetworkAdapterService adapterService;

    public NetworkService(INetworkAdapterService adapterService)
    {
        this.adapterService = adapterService;
    }

    /// <summary>
    /// 获取系统存储过的所有逻辑网络。
    /// </summary>
    /// <returns></returns>
    private static List<INetwork> GetAllINetworks()
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

    public List<Models.Network> GetAllNetworks()
    {
        var networks = GetAllINetworks();
        var result = new List<Models.Network>();

        foreach (var network in networks)
        {
            result.Add(new Models.Network
            {
                IsConnected = network.IsConnected,
                Id = network.GetNetworkId(),
                Name = network.GetName() ?? "未知网络",
            });
        }

        return result;
    }

    /// <summary>
    /// 获取输入逻辑网络下的所有网络连接。
    /// </summary>
    /// <param name="network">要操作的逻辑网络</param>
    /// <returns></returns>
    private static List<INetworkConnection> GetAllINetworkConnections(INetwork network)
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
    public List<Models.Network> GetAllEthernetAdapterNetworks()
    {
        return GetAllEthernetAdapterNetworks(GetAllINetworks());
    }

    /// <summary>
    /// 获取所有接入了以太网的逻辑网络。
    /// </summary>
    /// <param name="networks">要过滤的逻辑网络</param>
    /// <returns></returns>
    public List<Models.Network> GetAllEthernetAdapterNetworks(List<INetwork> networks)
    {
        var physicalIds = new HashSet<Guid>();
        foreach (var ethernet in adapterService.GetPhysicalEthernetAdapters())
        {
            if (ethernet.OperationalStatus == OperationalStatus.Up && Guid.TryParse(ethernet.Id, out var id))
            {
                physicalIds.Add(id);
            }
        }

        if (physicalIds.Count == 0)
            return [];

        var result = new List<Models.Network>();

        foreach (var network in networks)
        {
            var connections = GetAllINetworkConnections(network);
            foreach (var connection in connections)
            {
                if (physicalIds.Contains(connection.GetAdapterId()))
                {
                    result.Add(new Models.Network
                    {
                        Name = network.GetName() ?? "未知网络",
                        Id = network.GetNetworkId(),
                        IsConnected = network.IsConnected,
                    });
                    break;
                }
            }
        }

        return result;
    }
}