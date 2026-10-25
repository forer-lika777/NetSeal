using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Ras;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace NetSeal.Service;

public class Worker : BackgroundService
{
    private const string ENTRY_NAME = "NetSeal Connection";

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
    private readonly ServiceState serviceState;

    public Worker(ILogger<Worker> logger, IRasDialer rasDialer, IAppSettings settings, ServiceState serviceState)
    {
        this.logger = logger;
        this.rasDialer = rasDialer;
        this.settings = settings;
        this.serviceState = serviceState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NetSeal 服务已启动。");

        await Task.Delay(3000, stoppingToken);

        int backoffSeconds = 5;
        bool serviceJustStarted = true;
        DateTime lastTick = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            await serviceState.WaitForReloadAsync(PollInterval, stoppingToken);

            DateTime now = DateTime.UtcNow;
            bool resumedFromSleep = now - lastTick > SleepGapThreshold;
            lastTick = now;

            if (resumedFromSleep)
                logger.LogInformation("检测到系统从睡眠中恢复。");

            try
            {
                if (await rasDialer.GetActiveRasConnectionAsync() is not null)
                {
                    serviceJustStarted = false;
                    continue;
                }

                bool shouldDial =
                    (serviceJustStarted && await settings.AutoConnectOnStartup.GetValueAsync()) ||
                    (resumedFromSleep && await settings.ReconnectOnWake.GetValueAsync()) ||
                    await settings.AutoConnectOnStartup.GetValueAsync();

                serviceJustStarted = false;

                if (!shouldDial)
                    continue;


                await rasDialer.RasConnectAsync(
                    null,
                    ENTRY_NAME,
                    "",
                    "",
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
}
