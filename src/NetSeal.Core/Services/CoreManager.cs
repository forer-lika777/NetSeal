using NetSeal.Core.Models;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace NetSeal.Core.Services;

public class CoreManager : ICoreManager
{
    private readonly INetworkService networkService;
    private readonly INetworkAdapterService adapterService;
    private readonly IRasDialer rasDialer;

    private NetworkStatus cachedStatus = new(); // 初始状态
    private readonly SemaphoreSlim statusLock = new(1, 1);
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private DateTime lastUpdateTime = DateTime.MinValue;
    private readonly Lock locker = new();

    public event EventHandler? NetworkStatusChanged;

    public CoreManager(INetworkService networkService, INetworkAdapterService adapterService, IRasDialer rasDialer)
    {
        this.networkService = networkService;
        this.adapterService = adapterService;
        this.rasDialer = rasDialer;
        NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
        _ = ForceRefreshAsync();
    }

    private async void NetworkChange_NetworkAddressChanged(object? sender, EventArgs e)
    {
        try
        {
            lock (locker)
            {
                var now = DateTime.UtcNow;
                if (now - lastUpdateTime < cooldown)
                    return;

                lastUpdateTime = now;
            }

            await statusLock.WaitAsync();

            try
            {
                await UpdateNetworkConnectionStatusAsync(cachedStatus);
                await UpdatePppoeConnectionStatusAsync(cachedStatus);
                NetworkStatusChanged?.Invoke(sender, EventArgs.Empty);
            }
            finally
            {
                statusLock.Release();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex, "网络状态更新失败");
        }
    }

    private async Task ForceRefreshAsync()
    {
        await statusLock.WaitAsync();
        try
        {
            await UpdateNetworkConnectionStatusAsync(cachedStatus);
            await UpdatePppoeConnectionStatusAsync(cachedStatus);
        }
        finally
        {
            statusLock.Release();
        }
    }

    public async Task<NetworkStatus> GetNetworkStatusAsync()
    {
        await statusLock.WaitAsync();

        try
        {
            return cachedStatus;
        }
        finally
        {
            statusLock.Release();
        }
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
            status.IsPppoeNetworkConnected = false;
            status.IsPppoeConnectionManagedByOutside = false;
            status.ActivePppoeConnectionEntryName = "";

            if (await networkService.GetConnectivityLevelAsync() == NetworkConnectivityLevel.InternetAccess)
            {
                status.IsNetworkConnectionManagedByOutside = true;
                status.IsNotPppoeNetworkConnection = true;
            }
        }
    }
}
