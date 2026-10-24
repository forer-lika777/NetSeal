using NetSeal.Core.Models;
using NetSeal.Core.Services.Ras;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetSeal.Core.Services.Network;

public class NetworkService : INetworkService
{
    private readonly INetworkAdapterService adapterService;
    private readonly IRasDialer rasDialer;

    public NetworkService(INetworkAdapterService adapterService, IRasDialer rasDialer)
    {
        this.adapterService = adapterService;
        this.rasDialer = rasDialer;
    }

    public bool IsEthernetConnected => adapterService.GetActivePhysicalEthernetAdapters().Any();

    public async Task<NetworkConnectivityLevel> GetConnectivityLevelAsync()
    {
        var connectivity = await Task.Run(() => ((INetworkListManager)new NetworkListManagerClass()).GetConnectivity());

        const NLM_CONNECTIVITY internet = NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_INTERNET
                                        | NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_INTERNET;
        const NLM_CONNECTIVITY local = NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_LOCALNETWORK
                                     | NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_LOCALNETWORK;
        const NLM_CONNECTIVITY subnet = NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_SUBNET
                                      | NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_SUBNET;

        if ((connectivity & internet) != 0)
            return NetworkConnectivityLevel.InternetAccess;
        if ((connectivity & local) != 0)
            return NetworkConnectivityLevel.ConstrainedInternetAccess;
        if ((connectivity & subnet) != 0)
            return NetworkConnectivityLevel.LocalAccess;

        return NetworkConnectivityLevel.None;
    }

    public async Task<List<NetworkAdapter>> GetActiveNetworkAdaptersAsync()
    {
        return await Task.Run(() =>
        {
            var guids = GetAllINetworks()
                .Where(n => n.IsConnected)
                .SelectMany(n => GetAllINetworkConnections(n))
                .Select(c => c.GetAdapterId())
                .ToHashSet();

            return adapterService.GetNetworkAdapters()
                .Where(a => Guid.TryParse(a.Id, out var id) && guids.Contains(id))
                .ToList();
        });
    }

    public async Task<List<string>> GetActiveNetworkAdapterNamesAsync() 
    {
        var adapters = await GetActiveNetworkAdaptersAsync();
        return adapters.Select(a => a.Name).ToList();
    }

    public async Task<List<Models.Network>> GetActiveNetworksAsync()
    {
        return (await GetAllNetworksAsync()).Where(x => x.IsConnected).ToList();
    }

    public async Task<List<string>> GetActiveNetworkNamesAsync()
    {
        var networks = await GetActiveNetworksAsync();
        return networks.Select(n => n.Name ?? "未知网络名称").ToList();
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

    public async Task<List<Models.Network>> GetAllNetworksAsync()
    {
        var networks = await Task.Run(() => GetAllINetworks());
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
    private List<Models.Network> GetAllEthernetAdapterNetworks(List<INetwork> networks)
    {
        var physicalIds = new HashSet<Guid>();
        foreach (var ethernet in adapterService.GetActivePhysicalEthernetAdapters())
        {
            if (Guid.TryParse(ethernet.Id, out var id))
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

    public async Task<List<Models.Network>> GetAllEthernetAdapterNetworksAsync()
    {
        return await Task.Run(() => GetAllEthernetAdapterNetworks());
    }

    //public async Task<ConnectionProfile?> GetInternetConnectionProfileAsync()
    //{
    //    return await Task.Run(() => NetworkInformation.GetInternetConnectionProfile());
    //}
}