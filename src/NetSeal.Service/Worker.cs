using NetSeal.Core.Models;
using NetSeal.Core.Services;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Ras;
using Serilog.Core;
using System.ComponentModel;

namespace NetSeal.Service;

public class Worker : BackgroundService
{
    /// <summary>
    /// 轮询间隔。共享配置与连接状态都在每一轮重新读取，UI 修改后最多一个间隔生效。
    /// </summary>
    private static TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 单轮实际耗时明显超过此阈值时，视为刚从睡眠/休眠中恢复。
    /// 服务进程在系统睡眠期间被冻结，唤醒后计时器的跳跃就是这个特征。
    /// </summary>
    private static readonly TimeSpan SleepGapThreshold = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 系统级别（相对用户级别而言）的拨号连接电话簿。
    /// </summary>
    private const string phoneBookPath = @"C:\ProgramData\Microsoft\Network\Connections\Pbk\rasphone.pbk";

    private readonly ILogger<Worker> logger;
    private readonly IRasDialer rasDialer;
    private readonly INetworkSettings settings;
    private readonly ICoreManager coreManager;
    private readonly ServiceState serviceState;
    private readonly TimeProvider timeProvider;

    // 线程同步锁
    private readonly object lockObject = new();

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

    /// <summary>
    /// 跟踪上一次连接是否成功，用于判断是否发生了意外断连。
    /// </summary>
    private bool lastConnectionSucceeded = false;

    private TaskCompletionSource<IpcResponse>? ConnectInvokedTcs = null;

    public Worker(ILogger<Worker> logger,
        IRasDialer rasDialer,
        INetworkSettings settings,
        ICoreManager coreManager,
        ServiceState serviceState,
        TimeProvider timeProvider)
    {
        this.logger = logger;
        this.rasDialer = rasDialer;
        this.settings = settings;
        this.coreManager = coreManager;
        this.serviceState = serviceState;
        this.timeProvider = timeProvider;
        coreManager.NetworkStatusChanged += CoreManager_NetworkStatusChanged;
    }

    private void CoreManager_NetworkStatusChanged(object? sender, EventArgs e)
    {
        logger.LogInformation("检测到网络环境变更。");
        serviceState.TriggerReload();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NetSeal 服务已启动。");

        logger.LogInformation("正在等待其他服务项启动...");
        await Task.Delay(TimeSpan.FromSeconds(3), timeProvider, stoppingToken).ConfigureAwait(false);

        logger.LogInformation("正在加载相关配置...");
        autoConnect = await settings.AutoConnectPolicy.GetValueAsync().ConfigureAwait(false);
        rasCredentials = await settings.RasCredentials.GetValueAsync().ConfigureAwait(false);
        selectedNetwork = await settings.SelectedNetwork.GetValueAsync().ConfigureAwait(false);

        EnsurePhonebookExists(phoneBookPath);
        await rasDialer.RasCreateEntryAsync(phoneBookPath, RasEntryName.NetSealServiceEntryName, "", "").ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await UpdateNetworkStatusAsync();

            bool shouldConnect = GetShouldConnect();

            if (!shouldConnect)
            {
                PollInterval = TimeSpan.FromMilliseconds(-1);
                serviceJustStarted = false;
            }
            else
            {
                bool canConnect = GetCanConnect();

                if (!canConnect)
                {
                    if (connectRequested)
                        ConnectInvokedTcs?.TrySetResult(IpcResponse.Fail("当前网络不符合连接要求。"));

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

            if (PollInterval.Milliseconds != -1)
                logger.LogDebug("正在等待下次轮询：{Interval} 秒。", PollInterval.TotalSeconds);
            else
                logger.LogInformation("轮询已暂停。");

            if (await serviceState.WaitForReloadAsync(PollInterval, stoppingToken).ConfigureAwait(false))
            {
                logger.LogInformation("由于触发器提前终止，轮询未完成等待。");
                connectRetryCount = -1;
            }
        }

        logger.LogInformation("NetSeal 服务已停止。");
    }

    public async void SystemPowerMonitor_PowerModeChanged(object? sender, PowerMode e)
    {
        try
        {
            if (e == PowerMode.Suspend || e == PowerMode.ModernStandby)
            {
                if (e == PowerMode.Suspend)
                    logger.LogInformation($"系统已进入睡眠状态。");
                else
                    logger.LogInformation($"系统已进入新型待机状态。");

                isSleeping = true;
                PollInterval = TimeSpan.FromMilliseconds(-1);
                coreManager.NetworkStatusChanged -= CoreManager_NetworkStatusChanged;
            }
            else if (e == PowerMode.ResumeFromSuspend || e == PowerMode.ResumeFromStandby)
            {
                if (e == PowerMode.ResumeFromSuspend)
                    logger.LogInformation($"系统已从睡眠状态恢复。");
                else
                    logger.LogInformation($"系统已从新型待机状态恢复。");

                isSleeping = false;
                resumedFromSleep = true;
                await Task.Delay(2000);
                await coreManager.ForceRefreshAsync();
                serviceState.TriggerReload();
                coreManager.NetworkStatusChanged += CoreManager_NetworkStatusChanged;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新网络状态时出现错误。");
        }
    }

    private void SetPoll(bool success, bool doRetry, Exception? ex = null)
    {
        if (!success)
        {
            lastConnectionSucceeded = false;

            if (ex != null)
                logger.LogError(ex, $"拨号连接失败：{ex?.Message}");

            if (ex != null)
                ConnectInvokedTcs?.TrySetResult(IpcResponse.Fail(ex.Message));

            if (doRetry)
            {
                if (connectRetryCount < connectRetryLimit)
                {
                    connectRetryCount++;

                    if (connectRetryCount == 0)
                        PollInterval = TimeSpan.FromSeconds(3);
                    else if (connectRetryCount >= 1)
                        PollInterval = PollInterval.Add(TimeSpan.FromSeconds(3));

                    logger.LogInformation("连接失败。当前重试次数为：{Count}。将在 {Seconds} 秒后重试。", connectRetryCount, PollInterval.TotalSeconds);
                }
                else if (connectRetryCount >= connectRetryLimit)
                {
                    logger.LogInformation("连接失败。当前已达最大重试限制次数：{Count}。", connectRetryCount);
                    PollInterval = TimeSpan.FromMilliseconds(-1);
                }
            }
            else
            {
                PollInterval = TimeSpan.FromMilliseconds(-1);
            }
        }
        else
        {
            logger.LogInformation("拨号连接成功。");

            ConnectInvokedTcs?.TrySetResult(IpcResponse.Ok());
            PollInterval = TimeSpan.FromMilliseconds(-1);
            lastConnectionSucceeded = true;
            serviceJustStarted = false;
            resumedFromSleep = false;
            connectRetryCount = -1;
        }
    }

    public async Task<IpcResponse> ConnectRequest(CancellationToken ct = default)
    {
        disconnectRequested = false;
        connectRequested = true;
        logger.LogInformation("用户请求连接。");

        ConnectInvokedTcs = new TaskCompletionSource<IpcResponse>();

        connectRetryCount = 0;

        serviceState.TriggerReload();

        try
        {
            return await ConnectInvokedTcs.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            ConnectInvokedTcs = null;
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

    public async Task UpdateNetworkStatusAsync()
    {
        try
        {
            logger.LogInformation("正在更新网络状态...");
            var status = await coreManager.GetNetworkStatusAsync();
            lock (lockObject)
            {
                isEthernetConnected = status.IsEthernetConnected;
                isPppoeNetworkConnected = status.IsPppoeNetworkConnected;
                isNotPppoeNetworkConnection = status.IsNotPppoeNetworkConnection;
                isPppoeNetworkManagedByOutside = status.IsPppoeConnectionManagedByOutside;
                isNetworkConnectionManagedByOutside = status.IsNetworkConnectionManagedByOutside;
                networks = status.ActiveEthernetNetworks;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新网络状态失败。");
        }
    }

    private bool GetShouldConnect()
    {
        lock (lockObject)
        {
            if (connectRequested)
            {
                connectRequested = false;
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

            // 服务刚启动
            if (serviceJustStarted && autoConnect.AutoConnectOnStartup)
            {
                logger.LogInformation("服务初启动，即将进行拨号连接。");
                return true;
            }

            if (resumedFromSleep && autoConnect.ReconnectOnWake)
            {
                logger.LogInformation("检测到系统从挂起/低功耗状态恢复，即将尝试自动连接。");
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

        if (selectedNetwork is not null && !networks.Any(n => n.Id == selectedNetwork.Id))
        {
            logger.LogWarning($"当前未连接到匹配网络：{selectedNetwork.ToString()}，已连接的网络项：{string.Join(",", networks.Select(n => n.ToString()))}");
            return false;
        }

        return true;
    }

    public void EnsurePhonebookExists(string phonebookPath)
    {
        // 1. 获取文件所在目录
        string? directory = Path.GetDirectoryName(phonebookPath);

        try
        {
            // 2. 如果目录不存在，创建目录
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 3. 如果 PBK 文件不存在，创建一个空的（系统会接管它）
            if (!File.Exists(phonebookPath))
            {
                // 写入一个极其基础的 PBK 头部，或者直接建个空文件
                // 当 RasSetEntryProperties 调用时，系统会自动往里补全格式
                File.WriteAllText(phonebookPath, "");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "写入文件受拒绝。当前进程不具备访问系统电话簿权限，请检查权限设置。");
        }
    }
}

public class CredentialsInvalidError : Exception
{
    public CredentialsInvalidError() : base() { }
    public CredentialsInvalidError(string message) : base(message) { }
}