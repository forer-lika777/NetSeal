using NetSeal.Core.Models;
using NetSeal.Core.Services;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace NetSeal.Service;

public class Worker : BackgroundService
{
    /// <summary>
    /// 轮询间隔。共享配置与连接状态都在每一轮重新读取，UI 修改后最多一个间隔生效。
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 单轮实际耗时明显超过 <see cref="PollInterval"/> 时，视为刚从睡眠/休眠中恢复。
    /// 服务进程在系统睡眠期间被冻结，唤醒后计时器的跳跃就是这个特征。
    /// </summary>
    private static readonly TimeSpan SleepGapThreshold = TimeSpan.FromSeconds(60);

    private readonly ILogger<Worker> logger;
    private readonly IRasDialer rasDialer;
    private readonly IAppSettings settings;
    private readonly ICoreManager coreManager;
    private readonly ServiceState serviceState;

    private AutoConnectPolicy autoConnect;
    private RasCredentials? rasCredentials = null;

    private bool serviceJustStarted = true;

    private DateTime lastCheckTime = DateTime.Now;

    private DateTime nextRetryTime = DateTime.MinValue;

    private bool isEthernetConnected = false;
    private bool isPppoeNetworkConnected = false;
    private bool isNotPppoeNetorkConnection = false;
    private bool isPppoeNetworkManagedByOutside = false;
    private bool isNetworkConnectionManagedByOutside = false;

    /// <summary>
    /// Represent whether user side has sent a disconnect request.
    /// ReconnectOnDisconnect policy should not be triggered while connection is disconnected by the user side.
    /// </summary>
    private bool disconnectRequested = false;

    public Worker(ILogger<Worker> logger, 
        IRasDialer rasDialer, 
        IAppSettings settings, 
        INetworkService networkService, 
        INetworkAdapterService networkAdapterService, 
        ICoreManager coreManager,
        ServiceState serviceState)
    {
        this.logger = logger;
        this.rasDialer = rasDialer;
        this.settings = settings;
        this.coreManager = coreManager;
        this.serviceState = serviceState;
        autoConnect = new AutoConnectPolicy();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NetSeal 服务已启动。");

        await Task.Delay(3000, stoppingToken);

        int backoffSeconds = 5;

        await UpdateNetworkStatusAsync();

        autoConnect = await settings.AutoConnectPolicy.GetValueAsync();
        rasCredentials = await settings.RasCredentials.GetValueAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            await serviceState.WaitForReloadAsync(PollInterval, stoppingToken);

            bool shouldConnect = GetShouldConnect();

            serviceJustStarted = false;

            if (!shouldConnect)
                continue;

            try
            {
                await rasDialer.RasConnectAsync(
                    null,
                    RasEntryName.NetSealServiceEntryName,
                    rasCredentials.Username,
                    rasCredentials.Password,
                    stoppingToken);

                logger.LogInformation("拨号连接成功。");
                backoffSeconds = 5;
            }
            catch (OperationCanceledException) { break; }
            catch (Win32Exception ex)
            {
                logger.LogError(ex, "拨号连接失败，{Seconds} 秒后重试。", backoffSeconds);
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), stoppingToken);
                backoffSeconds = Math.Min(backoffSeconds * 2, 300);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "服务循环发生异常。");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("NetSeal 服务已停止。");
    }

    public void ConnectRequest()
    {
        disconnectRequested = false;
    }

    public void DisconnectRequest()
    {
        disconnectRequested = true;

    }

    public async Task UpdateAutoConnectPolicyAsync()
    {
        autoConnect = await settings.AutoConnectPolicy.GetValueAsync();
        if (autoConnect.ReconnectOnDisconnect)
            serviceState.TriggerReload();
    }

    public async Task UpdateRasCredentrialsAsync()
    {
        rasCredentials = await settings.RasCredentials.GetValueAsync();
        serviceState.TriggerReload();
    }

    private async Task UpdateNetworkStatusAsync()
    {
        var status = await coreManager.GetNetworkStatusAsync();
        isEthernetConnected = status.IsEthernetConnected;
        isPppoeNetworkConnected = status.IsPppoeNetworkConnected;
        isNotPppoeNetorkConnection = status.IsNotPppoeNetworkConnection;
        isPppoeNetworkManagedByOutside = status.IsPppoeConnectionManagedByOutside;
        isNetworkConnectionManagedByOutside = status.IsNetworkConnectionManagedByOutside;
    }

    private bool GetShouldConnect()
    {
        if (isPppoeNetworkConnected)
        {
            if (serviceJustStarted)
                logger.LogInformation("当前已连接到 pppoe 网络。");

            return false;
        }

        var now = DateTime.Now;
        var timeDrift = now - lastCheckTime;
        lastCheckTime = now;

        bool resumedFromSleep = timeDrift.TotalSeconds > 15;

        if (resumedFromSleep)
        {
            logger.LogInformation("检测到系统从睡眠中恢复。");
            if (autoConnect.ReconnectOnWake)
                return GetCanConnect();
        }
        else
        {
            if (serviceJustStarted)
            {
                if (autoConnect.AutoConnectOnStartup)
                    return GetCanConnect();
            }
            else
            {
                if (disconnectRequested)
                    return false;

                logger.LogError("检测到意外的断连");
                if (autoConnect.ReconnectOnDisconnect)
                    return GetCanConnect();
            }
        }

        return false;
    }

    private bool GetCanConnect()
    {
        if (!isEthernetConnected)
        {
            logger.LogInformation("当前未连接到有线网络。");
            return false;
        }

        if (isNetworkConnectionManagedByOutside)
        {
            logger.LogInformation("当前网络由外部管理。");
            return false;
        }

        if (isPppoeNetworkManagedByOutside)
        {
            logger.LogInformation("当前 pppoe 网络由外部管理。");
            return false;
        }

        return true;
    }
}
