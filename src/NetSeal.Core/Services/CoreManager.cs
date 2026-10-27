using NetSeal.Core.Models;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Services;

public class CoreManager : ICoreManager
{
    private readonly INetworkService networkService;
    private readonly INetworkAdapterService adapterService;
    private readonly IRasDialer rasDialer;

    public CoreManager(INetworkService networkService, INetworkAdapterService adapterService, IRasDialer rasDialer)
    {
        this.networkService = networkService;
        this.adapterService = adapterService;
        this.rasDialer = rasDialer;
    }

    public async Task<NetworkStatus> GetNetworkStatusAsync()
    {
        var networkStatus = new NetworkStatus();

        await UpdateNetworkConnectionStatusAsync(networkStatus);
        await UpdatePppoeConnectionStatusAsync(networkStatus);

        return networkStatus;
    }

    private async Task UpdateNetworkConnectionStatusAsync(NetworkStatus status)
    {
        status.IsEthernetConnected = networkService.IsEthernetConnected;

        status.ActiveEthernetNetworks = await networkService.GetAllActiveEthernetAdapterNetworksAsync();
        status.ActiveNetworkAdapterNames = string.Join(",", await networkService.GetActiveNetworkAdapterNamesAsync());
        status.ActiveNetworkNames = string.Join(",", await networkService.GetActiveNetworkNamesAsync());

        var level = await networkService.GetConnectivityLevelAsync();

        status.IsInternetAccess = level == NetworkConnectivityLevel.InternetAccess;
        status.NetworkConnectivityLevel = level;
    }

    private async Task UpdatePppoeConnectionStatusAsync(NetworkStatus status)
    {
        var connection = await rasDialer.GetActiveRasConnectionAsync();
        if (connection is not null)
        {
            status.IsPppoeNetworkConnected = true;

            if (connection.EntryName != RasEntryName.NetSealServiceEntryName)
            {
                status.IsPppoeConnectionManagedByOutside = true;
                status.IsNetworkConnectionManagedByOutside = true;
            }
            else
            {
                status.ActivePppoeConnectionEntryName = connection.EntryName;
            }
        }
        else
        {
            if (await networkService.GetConnectivityLevelAsync() == NetworkConnectivityLevel.InternetAccess)
            {
                status.IsNotPppoeNetworkConnection = true;
            }
        }
    }
}
