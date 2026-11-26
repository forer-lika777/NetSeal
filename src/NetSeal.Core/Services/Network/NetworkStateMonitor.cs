using Microsoft.Extensions.Logging;
using NetSeal.Core.Models;
using NetSeal.Core.Services.Ras;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace NetSeal.Core.Services.Network;

public class NetworkStateMonitor : INetworkStateMonitor
{
    private readonly ILogger<INetworkStateMonitor> logger;
    private readonly INetworkService networkService;
    private readonly INetworkAdapterService adapterService;
    private readonly IRasDialer rasDialer;

    private readonly NetworkState cachedState = new(); // 初始状态
    private readonly SemaphoreSlim stateLock = new(2, 2);
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private DateTime lastUpdateTime = DateTime.MinValue;

    private bool registered = false;

    public event EventHandler? NetworkStateChanged;

    public NetworkStateMonitor(
        INetworkService networkService,
        INetworkAdapterService adapterService,
        IRasDialer rasDialer,
        ILogger<INetworkStateMonitor> logger)
    {
        this.networkService = networkService;
        this.adapterService = adapterService;
        this.rasDialer = rasDialer;
        this.logger = logger;
    }

    private CancellationTokenSource? debounceCts = null;

    private async void NetworkChange_NetworkAddressChanged(object? sender, EventArgs e)
    {
        try
        {
            logger.LogDebug("收到网络环境变更事件。");

            await stateLock.WaitAsync();

            try
            {
                var now = DateTime.UtcNow;
                var gap = now - lastUpdateTime;
                lastUpdateTime = now;

                if (gap < cooldown)
                {
                    lastUpdateTime = now;
                    ResetCts(ref debounceCts);

                    try
                    {
                        await Task.Delay(cooldown, debounceCts!.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogDebug("距离上次检查仍在冷却时间内。此线程已退出。");
                        return;
                    }
                }

                logger.LogInformation("收到网络变更事件。正在更新网络状态...");

                await UpdateNetworkConnectionStateAsync(cachedState);
                await UpdatePppoeConnectionStateAsync(cachedState);

                NetworkStateChanged?.Invoke(sender, EventArgs.Empty);
            }
            finally
            {
                stateLock.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "网络状态更新失败");
        }
    }

    /// <summary>
    /// 强制更新网络状态。此函数入口在更新后不会触发 <see cref="NetworkStateChanged"/> 事件通知。
    /// </summary>
    /// <returns></returns>
    public async Task ForceRefreshAsync()
    {
        await stateLock.WaitAsync();

        try
        {
            logger.LogInformation("正在更新网络状态（外部强制更新）...");
            await UpdateNetworkConnectionStateAsync(cachedState);
            await UpdatePppoeConnectionStateAsync(cachedState);
        }
        finally
        {
            stateLock.Release();
        }
    }

    public async Task<NetworkState> GetNetworkStateAsync()
    {
        await stateLock.WaitAsync();

        try
        {
            return cachedState;
        }
        finally
        {
            stateLock.Release();
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

    private async Task UpdateNetworkConnectionStateAsync(NetworkState state)
    {
        state.IsEthernetConnected = networkService.IsEthernetConnected;

        state.ActiveEthernetNetworks = await networkService.GetAllActiveEthernetAdapterNetworksAsync();
        state.ActiveNetworkAdapterNames = string.Join(",", await networkService.GetActiveNetworkAdapterNamesAsync());
        state.ActiveNetworkNames = string.Join(",", await networkService.GetActiveNetworkNamesAsync());

        var level = await networkService.GetConnectivityLevelAsync();

        state.IsInternetAccess = level == NetworkConnectivityLevel.InternetAccess;
        state.NetworkConnectivityLevel = level;
    }

    private async Task UpdatePppoeConnectionStateAsync(NetworkState state)
    {
        var connection = await rasDialer.GetActiveRasConnectionAsync();
        if (connection is not null)
        {
            state.IsPppoeNetworkConnected = true;

            state.ActivePppoeConnectionEntryName = connection.EntryName;

            if (connection.EntryName == RasEntryName.NetSealServiceEntryName)
            {
                state.IsPppoeConnectionManagedByOutside = false;
                state.IsNetworkConnectionManagedByOutside = false;
            }
            else
            {
                state.IsNetworkConnectionManagedByOutside = true;
                state.IsPppoeConnectionManagedByOutside = true;
            }
        }
        else
        {
            state.IsPppoeNetworkConnected = false;
            state.IsPppoeConnectionManagedByOutside = false;
            state.ActivePppoeConnectionEntryName = "";

            if (await networkService.GetConnectivityLevelAsync() == NetworkConnectivityLevel.InternetAccess)
            {
                state.IsNetworkConnectionManagedByOutside = true;
                state.IsNotPppoeNetworkConnection = true;
            }
            else
            {
                state.IsNetworkConnectionManagedByOutside = false;
                state.IsNotPppoeNetworkConnection = false;
            }
        }
    }

    private static void ResetCts(ref CancellationTokenSource? cts, CancellationTokenSource? ctsToUse = null)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = ctsToUse ?? new CancellationTokenSource();
    }
}
