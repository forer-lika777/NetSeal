using NetSeal.Core.Models;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using System.ComponentModel;

namespace NetSeal.Service;

public class Worker : BackgroundService
{
    /// <summary>
    /// 轮询间隔。共享配置与连接状态都在每一轮重新读取，UI 修改后最多一个间隔生效。
    /// </summary>
    private TimeSpan pollInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 系统级别（相对用户级别而言）的拨号连接电话簿。
    /// </summary>
    private const string phoneBookPath = @"C:\ProgramData\Microsoft\Network\Connections\Pbk\rasphone.pbk";

    private readonly ILogger<Worker> logger;
    private readonly IRasDialer rasDialer;
    private readonly INetworkSettings settings;
    private readonly INetworkStateMonitor coreManager;
    private readonly ServiceState serviceState;
    private readonly TimeProvider timeProvider;

    // 线程同步锁
    private readonly Lock lockObject = new();

    private AutoConnectPolicy autoConnect = new();
    private RasCredentials? rasCredentials = null;
    private NetworkItem? selectedNetwork = null;

    private bool serviceJustStarted = true;
    private int connectRetryCount = -1;
    private const int connectRetryLimit = 5;

    // 网络状态（受线程同步保护）
    private List<NetworkItem> networks = [];
    private bool isEthernetConnected = false;
    private bool isPppoeNetworkConnected = false;
    private bool isNotPppoeNetworkConnection = false;
    private bool isPppoeNetworkManagedByOutside = false;
    private bool isNetworkConnectionManagedByOutside = false;

    /// <summary>
    /// Windows 睡着了
    /// </summary>
    private bool isSleeping = false;
    private bool resumedFromSleep = false;

    /// <summary>
    /// 表示用户是否已请求断开连接。
    /// 当用户请求断开时，不应触发自动重连策略。
    /// </summary>
    private bool disconnectRequested = false;

    private bool connectRequested = false;

    private bool hasTriedDetectedConnect = false;

    /// <summary>
    /// 跟踪上一次连接是否成功，用于判断是否发生了意外断连。
    /// </summary>
    private bool lastConnectionSucceeded = false;

    private TaskCompletionSource<IpcResponse>? connectInvokedTcs = null;

    public Worker(ILogger<Worker> logger,
        IRasDialer rasDialer,
        INetworkSettings settings,
        INetworkStateMonitor coreManager,
        ServiceState serviceState,
        TimeProvider timeProvider)
    {
        this.logger = logger;
        this.rasDialer = rasDialer;
        this.settings = settings;
        this.coreManager = coreManager;
        this.serviceState = serviceState;
        this.timeProvider = timeProvider;
        coreManager.NetworkStateChanged += CoreNetworkManager_NetworkStateChanged;
    }

    private void CoreNetworkManager_NetworkStateChanged(object? sender, EventArgs e)
    {
        if (isSleeping)
            return;

        serviceState.TriggerReload();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NetSeal 服务已启动。");

        logger.LogInformation("正在等待其他服务项启动...");
        await Task.Delay(TimeSpan.FromSeconds(3), timeProvider, stoppingToken).ConfigureAwait(false);

        logger.LogInformation("正在加载相关配置...");
        autoConnect = await settings.AutoConnectPolicy.GetValueAsync(stoppingToken).ConfigureAwait(false);
        rasCredentials = await settings.RasCredentials.GetValueAsync(stoppingToken).ConfigureAwait(false);
        selectedNetwork = await settings.SelectedNetwork.GetValueAsync(stoppingToken).ConfigureAwait(false);

        EnsurePhonebookExists(phoneBookPath);
        await rasDialer.RasCreateEntryAsync(phoneBookPath, RasEntryName.NetSealServiceEntryName, "", "").ConfigureAwait(false);

        coreManager.Register();
        await coreManager.ForceRefreshAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            await UpdateNetworkStateAsync();

            bool shouldConnect = GetShouldConnect();

            if (!shouldConnect)
            {
                pollInterval = TimeSpan.FromMilliseconds(-1);
                serviceJustStarted = false;
            }
            else
            {
                bool canConnect = GetCanConnect();

                if (!canConnect)
                {
                    if (connectRequested)
                        connectInvokedTcs?.TrySetResult(IpcResponse.Fail("当前网络不符合连接要求。"));

                    logger.LogWarning("当前网络不符合连接要求，正在等待下一次检查。");
                    SetPoll(false, true);
                }
                else
                {
                    try
                    {
                        if (rasCredentials is null)
                            throw new CredentialsInvalidError("当前还未配置凭据设置。");

                        logger.LogInformation("正在执行拨号连接操作...");

                        await rasDialer.RasConnectAsync(
                            phoneBookPath,
                            RasEntryName.NetSealServiceEntryName,
                            rasCredentials.Username,
                            rasCredentials.Password,
                            stoppingToken);

                        SetPoll(true, false);

                        _ = WaitAndManuallyComfirmNetworkStateAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Win32Exception ex)
                    {
                        SetPoll(false, true, ex);
                    }
                    catch (AggregateException ex)
                    {
                        SetPoll(false, true, ex);
                    }
                    catch (CredentialsInvalidError ex)
                    {
                        SetPoll(false, false, ex);
                    }
                    catch (Exception ex)
                    {
                        SetPoll(false, true, ex);
                    }
                }
            }

            if (pollInterval.Milliseconds != -1)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("正在等待下次轮询：{Interval} 秒。", pollInterval.TotalSeconds);
            }
            else
            {
                logger.LogInformation("轮询已暂停。");
            }

            if (await serviceState.WaitForReloadAsync(pollInterval, stoppingToken).ConfigureAwait(false))
            {
                logger.LogInformation("由于触发器提前终止，轮询未完成等待。");
                connectRetryCount = -1;
            }
        }

        logger.LogInformation("NetSeal 服务已停止。");
    }

    private bool GetShouldConnect()
    {
        lock (lockObject)
        {
            if (connectRequested)
            {
                connectRequested = false;
                logger.LogWarning("用户正在请求连接。");
                return true;
            }

            if (isSleeping)
            {
                logger.LogWarning("Windows 正在睡觉，请不要打扰他。");
                return false;
            }

            // 已经连接 PPPoE 网络
            if (isPppoeNetworkConnected)
            {
                logger.LogInformation("当前已连接到 PPPoE 网络，不需要进行连接。");
                return false;
            }

            if (isNetworkConnectionManagedByOutside)
            {
                logger.LogWarning("当前网络由外部管理。");
                return false;
            }

            if (isPppoeNetworkManagedByOutside)
            {
                logger.LogWarning("当前 PPPoE 网络由外部管理。");
                return false;
            }

            // 服务刚启动
            if (serviceJustStarted && autoConnect.AutoConnectOnStartup)
            {
                logger.LogInformation("服务初启动，即将进行拨号连接。");
                return true;
            }

            if (resumedFromSleep && autoConnect.ReconnectOnWake)
            {
                logger.LogInformation("系统从挂起/低功耗状态恢复，即将尝试自动连接。");
                return true;
            }

            // 检测到意外的断连（连接曾经成功过，但现在断开了）
            if (!disconnectRequested && lastConnectionSucceeded && !isPppoeNetworkConnected && autoConnect.ReconnectOnDisconnect)
            {
                logger.LogWarning("检测到意外的断连。正在尝试重新连接...");
                return true;
            }

            return false;
        }
    }

    private bool GetCanConnect()
    {
        if (!isEthernetConnected)
        {
            logger.LogWarning("当前未连接到有线网络。");
            return false;
        }

        if (selectedNetwork is not null && !networks.Any(n => n.Id == selectedNetwork.Id))
        {
            logger.LogWarning("当前未连接到匹配网络：{selectedNetwork}，已连接的网络项：{networks}", selectedNetwork, string.Join(",", networks.Select(n => n.ToString())));

            if (!hasTriedDetectedConnect)
            {
                logger.LogInformation("尝试探测性连接（这是为了避免可能存在的网络识别过程中产生的延迟）...");
                hasTriedDetectedConnect = true;
                return true;
            }

            return false;
        }

        return true;
    }

    public async void SystemPowerMonitor_PowerModeChanged(object? _, PowerMode e)
    {
        try
        {
            if (e == PowerMode.Suspend || e == PowerMode.ModernStandby)
            {
                if (e == PowerMode.Suspend)
                    logger.LogInformation($"系统已进入睡眠（挂起）状态。");
                else
                    logger.LogInformation($"系统已进入新型待机状态。");

                coreManager.Unregister();
                isSleeping = true;
                pollInterval = TimeSpan.FromMilliseconds(-1);
            }
            else if (e == PowerMode.ResumeFromSuspend || e == PowerMode.ResumeFromStandby)
            {
                if (e == PowerMode.ResumeFromSuspend)
                    logger.LogInformation($"系统已从睡眠（挂起）状态恢复。");
                else
                    logger.LogInformation($"系统已从新型待机状态恢复。");

                isSleeping = false;
                resumedFromSleep = true;
                hasTriedDetectedConnect = false;
                await coreManager.ForceRefreshAsync();
                coreManager.Register();
                serviceState.TriggerReload();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新电源状态时出现错误。");
        }
    }

    private void SetPoll(bool success, bool doRetry, Exception? ex = null)
    {
        if (!success)
        {
            lastConnectionSucceeded = false;

            if (ex != null)
                logger.LogError(ex, "拨号连接失败：{Message}", ex?.Message);

            if (ex != null)
                connectInvokedTcs?.TrySetResult(IpcResponse.Fail(ex.Message));

            if (doRetry)
            {
                if (connectRetryCount < connectRetryLimit)
                {
                    connectRetryCount++;

                    if (connectRetryCount == 0)
                        pollInterval = TimeSpan.FromSeconds(3);

                    if (logger.IsEnabled(LogLevel.Information))
                        logger.LogInformation("连接失败。当前重试次数为：{Count}。将在 {Seconds} 秒后重试。", connectRetryCount, pollInterval.TotalSeconds);
                }
                else if (connectRetryCount >= connectRetryLimit)
                {
                    if (logger.IsEnabled(LogLevel.Information))
                        logger.LogInformation("连接失败。当前已达最大重试限制次数：{Count}。", connectRetryCount);

                    hasTriedDetectedConnect = false;

                    pollInterval = TimeSpan.FromMilliseconds(-1);
                }
            }
            else
            {
                pollInterval = TimeSpan.FromMilliseconds(-1);
            }
        }
        else
        {
            logger.LogInformation("拨号连接成功。");

            connectInvokedTcs?.TrySetResult(IpcResponse.Ok());

            pollInterval = TimeSpan.FromMilliseconds(-1);

            serviceJustStarted = false;
            resumedFromSleep = false;

            lastConnectionSucceeded = true;
            hasTriedDetectedConnect = false;

            connectRetryCount = -1;
        }
    }

    private async Task WaitAndManuallyComfirmNetworkStateAsync()
    {
        // 拨号连接成功后，会触发多个网络变更事件（测试统计为 3 次）。
        // 此处临时屏蔽网络变更事件，并在 10 秒后手动触发检查。
        coreManager.Unregister();

        logger.LogDebug("已取消网络变更事件订阅，并于 10 秒后检查确保网络状态并重新订阅。");

        await Task.Delay(10000);

        await coreManager.ForceRefreshAsync();

        coreManager.Register();

        serviceState.TriggerReload();
    }

    public async Task<IpcResponse> ConnectRequest(CancellationToken ct = default)
    {
        lock (lockObject)
        {
            disconnectRequested = false;
            connectRequested = true;
        }
        
        logger.LogInformation("用户请求连接。");

        connectInvokedTcs = new TaskCompletionSource<IpcResponse>();

        connectRetryCount = 0;

        serviceState.TriggerReload();

        try
        {
            return await connectInvokedTcs.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            connectInvokedTcs = null;
        }
    }

    public async Task<IpcResponse> DisconnectRequest(CancellationToken ct = default)
    {
        logger.LogInformation("用户请求断开连接。");
        disconnectRequested = true;
        try
        {
            await rasDialer.RasDisconnectAsync(null, ct);
        }
        catch (Win32Exception ex)
        {
            logger.LogError(ex, "断开连接失败：");
            return IpcResponse.Fail(ex.Message);
        }

        return IpcResponse.Ok();
    }

    public async Task UpdateAutoConnectPolicyAsync()
    {
        var newPolicy = await settings.AutoConnectPolicy.GetValueAsync();
        lock (lockObject)
        {
            logger.LogInformation("正在更新自动连接策略...");

            if (autoConnect.ReconnectOnDisconnect != newPolicy.ReconnectOnDisconnect && newPolicy.ReconnectOnDisconnect)
            {
                disconnectRequested = false;
                autoConnect = newPolicy;
                serviceState.TriggerReload();
            }
            else if (autoConnect.AutoConnectOnStartup != newPolicy.AutoConnectOnStartup && newPolicy.AutoConnectOnStartup)
            {
                disconnectRequested = false;
                autoConnect = newPolicy;
                serviceState.TriggerReload();
            }
            else
            {
                autoConnect = newPolicy;
            }
        }
    }

    public async Task UpdateRasCredentrialsAsync()
    {
        await settings.HasRasCredentials.SetValueAsync(true);
        var newCredentials = await settings.RasCredentials.GetValueAsync();
        lock (lockObject)
        {
            logger.LogInformation("正在更新凭据...");
            rasCredentials = newCredentials;
        }

        serviceState.TriggerReload();
    }

    public async Task UpdateSelectedNetworkAsync()
    {
        var network = await settings.SelectedNetwork.GetValueAsync();
        lock (lockObject)
        {
            logger.LogInformation("正在更新已选网络...");
            selectedNetwork = network;
        }

        serviceState.TriggerReload();
    }

    public async Task UpdateNetworkStateAsync()
    {
        try
        {
            logger.LogInformation("正在检查网络状态...");
            var state = await coreManager.GetNetworkStateAsync();
            lock (lockObject)
            {
                isEthernetConnected = state.IsEthernetConnected;
                isPppoeNetworkConnected = state.IsPppoeNetworkConnected;
                isNotPppoeNetworkConnection = state.IsNotPppoeNetworkConnection;
                isPppoeNetworkManagedByOutside = state.IsPppoeConnectionManagedByOutside;
                isNetworkConnectionManagedByOutside = state.IsNetworkConnectionManagedByOutside;
                networks = state.ActiveEthernetNetworks;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新网络状态失败。");
        }
    }

    public void EnsurePhonebookExists(string phonebookPath)
    {
        string? directory = Path.GetDirectoryName(phonebookPath);

        try
        {
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(phonebookPath))
                File.WriteAllText(phonebookPath, "");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "写入文件受拒绝。当前进程不具备访问系统电话簿权限，请检查权限设置。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "写入文件时失败。");
        }
    }
}

public class CredentialsInvalidError : Exception
{
    public CredentialsInvalidError() : base() { }
    public CredentialsInvalidError(string message) : base(message) { }
}