using NetSeal.Core.Models;
using NetSeal.Core.Services;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Pipe;
using NetSeal.Core.Services.Ras;
using System.ComponentModel;

namespace NetSeal.Service;

public class Worker : BackgroundService
{
    /// <summary>
    /// 轮询间隔。共享配置与连接状态都在每一轮重新读取，UI 修改后最多一个间隔生效。
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 单轮实际耗时明显超过此阈值时，视为刚从睡眠/休眠中恢复。
    /// 服务进程在系统睡眠期间被冻结，唤醒后计时器的跳跃就是这个特征。
    /// </summary>
    private static readonly TimeSpan SleepGapThreshold = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 连接请求等待超时时间
    /// </summary>
    //private static readonly TimeSpan ConnectRequestTimeout = TimeSpan.FromSeconds(30);

    private const string phoneBookPath = @"C:\ProgramData\Microsoft\Network\Connections\Pbk\rasphone.pbk";

    private readonly ILogger<Worker> logger;
    private readonly IRasDialer rasDialer;
    private readonly IAppSettings settings;
    private readonly ICoreManager coreManager;
    private readonly ServiceState serviceState;

    // 线程同步锁
    private readonly object lockObject = new();

    private AutoConnectPolicy autoConnect = new();
    private RasCredentials? rasCredentials = null;

    private bool serviceJustStarted = true;
    private int connectRetryCount = -1;
    private const int connectRetryLimit = 5;
    private DateTime lastCheckTime = DateTime.Now;
    //private DateTime nextRetryTime = DateTime.MinValue;
    //private int backoffSeconds = 0;

    // 网络状态（受线程同步保护）
    private bool isEthernetConnected = false;
    private bool isPppoeNetworkConnected = false;
    private bool isNotPppoeNetworkConnection = false;
    private bool isPppoeNetworkManagedByOutside = false;
    private bool isNetworkConnectionManagedByOutside = false;

    /// <summary>
    /// 表示用户是否已请求断开连接。
    /// 当用户请求断开时，不应触发自动重连策略。
    /// </summary>
    private bool disconnectRequested = false;

    /// <summary>
    /// 跟踪上一次连接是否成功，用于判断是否发生了意外断连。
    /// </summary>
    private bool lastConnectionSucceeded = false;

    /// <summary>
    /// 当前的连接操作 TaskCompletionSource，用于等待连接结果
    /// 受 lockObject 保护
    /// </summary>
    //private TaskCompletionSource<IpcResponse>? pendingConnectRequest = null;

    /// <summary>
    /// 最后一次连接的结果（用于调试和日志）
    /// 受 lockObject 保护
    /// </summary>
    private IpcResponse? lastConnectResult = null;

    private TaskCompletionSource<IpcResponse>? ConnectInvokedTcs = null;
    //private SemaphoreSlim ConnectInvokeLocker = new SemaphoreSlim(1, 1);

    public Worker(ILogger<Worker> logger, 
        IRasDialer rasDialer, 
        IAppSettings settings, 
        ICoreManager coreManager,
        ServiceState serviceState)
    {
        this.logger = logger;
        this.rasDialer = rasDialer;
        this.settings = settings;
        this.coreManager = coreManager;
        this.serviceState = serviceState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NetSeal 服务已启动。");

        logger.LogInformation("正在等待其他服务项启动...");
        await Task.Delay(3000, stoppingToken);

        logger.LogInformation("正在加载网络信息...");
        await UpdateNetworkStatusAsync();

        logger.LogInformation("正在加载相关配置...");
        autoConnect = await settings.AutoConnectPolicy.GetValueAsync();
        rasCredentials = await settings.RasCredentials.GetValueAsync();

        EnsurePhonebookExists(phoneBookPath);
        await rasDialer.RasCreateEntryAsync(phoneBookPath, RasEntryName.NetSealServiceEntryName, "", "");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!serviceJustStarted)
                await serviceState.WaitForReloadAsync(PollInterval, stoppingToken);

            // 每次循环都更新网络状态
            await UpdateNetworkStatusAsync();

            bool shouldConnect = GetShouldConnect();

            serviceJustStarted = false;

            if (!shouldConnect)
                continue;

            try
            {
                if (string.IsNullOrWhiteSpace(rasCredentials.Username) || string.IsNullOrWhiteSpace(rasCredentials.Password))
                {
                    throw new CredentialsInvalidError("当前本地配置项的凭据设置不正确（账户名或密码为空）。");
                }

                await rasDialer.RasConnectAsync(
                    phoneBookPath,
                    RasEntryName.NetSealServiceEntryName,
                    rasCredentials.Username,
                    rasCredentials.Password,
                    stoppingToken);

                logger.LogInformation("拨号连接成功。");
                connectRetryCount = -1;
                lastConnectionSucceeded = true;
                //backoffSeconds = 0;
                //nextRetryTime = DateTime.MinValue;
                //ConnectInvokeLocker.Wait();
                ConnectInvokedTcs?.TrySetResult(new IpcResponse { Success = true });
            }
            catch (OperationCanceledException) 
            { 
                break; 
            }
            catch (Win32Exception ex)
            {
                SetRetryFailed(ex);
            }
            catch (AggregateException ex)
            {
                SetRetryFailed(ex);
            }
            catch (CredentialsInvalidError ex)
            {
                lastConnectionSucceeded = false;
                logger.LogError(ex, "凭据配置错误。");
                ConnectInvokedTcs?.TrySetResult(new IpcResponse { Success = false, Error = ex.Message });
            }
            catch (Exception ex)
            {
                lastConnectionSucceeded = false;
                logger.LogError(ex, "服务循环发生异常。");
                
                //nextRetryTime = DateTime.Now.AddSeconds(5);
                ConnectInvokedTcs?.TrySetResult(new IpcResponse { Success = false, Error = ex.Message });
            }
        }

        logger.LogInformation("NetSeal 服务已停止。");
    }

    private void SetRetryFailed(Exception ex)
    {
        lastConnectionSucceeded = false;
        logger.LogError(ex, "拨号连接失败。");

        if (connectRetryCount == connectRetryLimit)
        {
            ConnectInvokedTcs?.TrySetResult(new IpcResponse { Success = false, Error = ex.Message });
            logger.LogInformation("连接失败。当前已达最大连接重试限制次数：{Count}。", connectRetryCount);
            connectRetryCount = -1;
        }
        else
        {
            connectRetryCount++;
            logger.LogInformation("连接失败。当前连接重试次数为：{Count}。", connectRetryCount);
        }

        ConnectInvokedTcs?.TrySetResult(new IpcResponse { Success = false, Error = ex.Message });
    }

    public async Task<IpcResponse> ConnectRequest(CancellationToken ct = default)
    {
        // 设置标志触发连接
        disconnectRequested = false;
        //lastConnectionSucceeded = true;
        logger.LogInformation("用户请求连接");

        ConnectInvokedTcs = new TaskCompletionSource<IpcResponse>();

        connectRetryCount = 0;

        // 触发 Worker 线程检查
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
        logger.LogInformation("用户请求断开连接");
        try
        {
            await rasDialer.RasDisconnectAsync(null, ct);
        }
        catch (Win32Exception ex)
        {
            return new IpcResponse { Success = false, Error = ex.Message };
        }

        disconnectRequested = true;

        return new IpcResponse { Success = true };
    }

    public IpcResponse? GetConnectStatus()
    {
        return lastConnectResult;
    }

    public async Task UpdateAutoConnectPolicyAsync()
    {
        var newPolicy = await settings.AutoConnectPolicy.GetValueAsync();
        lock (lockObject)
        {
            if (autoConnect.ReconnectOnDisconnect != newPolicy.ReconnectOnDisconnect && newPolicy.ReconnectOnDisconnect)
            {
                disconnectRequested = false;
                serviceState.TriggerReload();
            }
            else if (autoConnect.AutoConnectOnStartup != newPolicy.AutoConnectOnStartup && newPolicy.AutoConnectOnStartup)
            {
                disconnectRequested = false;
                serviceState.TriggerReload();
            }


            autoConnect = newPolicy;
        }
    }

    public async Task UpdateRasCredentrialsAsync()
    {
        var newCredentials = await settings.RasCredentials.GetValueAsync();
        lock (lockObject)
        {
            rasCredentials = newCredentials;
        }
        serviceState.TriggerReload();
    }

    private async Task UpdateNetworkStatusAsync()
    {
        try
        {
            var status = await coreManager.GetNetworkStatusAsync();
            lock (lockObject)
            {
                isEthernetConnected = status.IsEthernetConnected;
                isPppoeNetworkConnected = status.IsPppoeNetworkConnected;
                isNotPppoeNetworkConnection = status.IsNotPppoeNetworkConnection;
                isPppoeNetworkManagedByOutside = status.IsPppoeConnectionManagedByOutside;
                isNetworkConnectionManagedByOutside = status.IsNetworkConnectionManagedByOutside;
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
            var now = DateTime.Now;
            var timeDrift = now - lastCheckTime;
            lastCheckTime = now;

            // 已经连接 PPPoE 网络
            if (isPppoeNetworkConnected)
            {
                //logger.LogInformation("当前已连接到 PPPoE 网络，不需要进行连接。");
                return false;
            }

            if (connectRetryCount >= 0 && connectRetryCount < connectRetryLimit)
            {
                return GetCanConnect();
            }
            else if (connectRetryCount > connectRetryLimit)
            {
                logger.LogInformation("已达次数上限。");
                return false;
            }

            // 服务刚启动
            if (serviceJustStarted && autoConnect.AutoConnectOnStartup)
            {
                logger.LogInformation("服务初启动，即将进行拨号连接。");
                return GetCanConnect();
            }

            // 检测系统是否从睡眠中恢复
            bool resumedFromSleep = timeDrift > SleepGapThreshold;

            if (resumedFromSleep && autoConnect.ReconnectOnWake)
            {
                logger.LogInformation("检测到系统从睡眠中恢复，即将尝试自动连接。");
                return GetCanConnect();
            }

            // 检测到意外的断连（连接曾经成功过，但现在断开了）
            if (!disconnectRequested && lastConnectionSucceeded && !isPppoeNetworkConnected && autoConnect.ReconnectOnDisconnect)
            {
                logger.LogInformation("检测到意外的断连。正在尝试重新连接...");
                return GetCanConnect();
            }

            return false;
        }
    }

    private bool GetCanConnect()
    {
        // 注意：这个方法应该在获得锁的情况下调用
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
            logger.LogInformation("当前 PPPoE 网络由外部管理。");
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