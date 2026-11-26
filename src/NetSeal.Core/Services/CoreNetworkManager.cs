using Microsoft.Extensions.Logging;
using NetSeal.Core.Models;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace NetSeal.Core.Services;

public class CoreNetworkManager : ICoreNetworkManager
{
    private readonly ILogger<ICoreNetworkManager> logger;
    private readonly INetworkService networkService;
    private readonly INetworkAdapterService adapterService;
    private readonly IRasDialer rasDialer;

    private readonly NetworkStatus cachedStatus = new(); // 初始状态
    private readonly SemaphoreSlim statusLock = new(1, 1);
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private DateTime lastUpdateTime = DateTime.MinValue;
    private readonly Lock locker = new();

    private bool registered = false;

    public event EventHandler? NetworkStatusChanged;

    public CoreNetworkManager(
        INetworkService networkService,
        INetworkAdapterService adapterService,
        IRasDialer rasDialer,
        ILogger<ICoreNetworkManager> logger)
    {
        this.networkService = networkService;
        this.adapterService = adapterService;
        this.rasDialer = rasDialer;
        this.logger = logger;
        _ = ForceRefreshAsync();
    }

    private async void NetworkChange_NetworkAddressChanged(object? sender, EventArgs e)
    {
        try
        {
            logger.LogDebug("收到网络环境变更事件。");
            lock (locker)
            {
                var now = DateTime.UtcNow;
                try
                {
                    if (now - lastUpdateTime < cooldown)
                    {
                        logger.LogDebug("距离上次检查仍在冷却时间内。此线程已退出。");
                        return;
                    }
                }
                finally
                {
                    lastUpdateTime = now;
                }
            }

            if (!await statusLock.WaitAsync(TimeSpan.FromSeconds(3)))
            {
                return;
            }

            try
            {
                logger.LogInformation("正在更新网络状态...");
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
            logger.LogError(ex, "网络状态更新失败");
        }
    }

    /// <summary>
    /// 强制更新网络状态。此函数入口在更新后不会触发 <see cref="NetworkStatusChanged"/> 事件通知。
    /// </summary>
    /// <returns></returns>
    public async Task ForceRefreshAsync()
    {
        await statusLock.WaitAsync();

        try
        {
            logger.LogInformation("正在更新网络状态（外部强制更新）...");
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

    public void Register()
    {
        if (!registered)
        {
            logger.LogInformation("已注册网络变更事件。");
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            registered = true;
        }
    }
    public void Unregister()
    {
        if (registered)
        {
            logger.LogInformation("已取消注册网络变更事件。");
            NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
            registered = false;
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

            status.ActivePppoeConnectionEntryName = connection.EntryName;

            if (connection.EntryName == RasEntryName.NetSealServiceEntryName)
            {
                status.IsPppoeConnectionManagedByOutside = false;
                status.IsNetworkConnectionManagedByOutside = false;
            }
            else
            {
                status.IsNetworkConnectionManagedByOutside = true;
                status.IsPppoeConnectionManagedByOutside = true;
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
            else
            {
                status.IsNetworkConnectionManagedByOutside = false;
                status.IsNotPppoeNetworkConnection = false;
            }
        }
    }
}
