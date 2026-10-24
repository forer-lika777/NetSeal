using NetSeal.Core.Services;
using NetSeal.Core.Services.Ras;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace NetSeal.Service;

[SupportedOSPlatform("windows")]
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

    public Worker(ILogger<Worker> logger, IRasDialer rasDialer)
    {
        this.logger = logger;
        this.rasDialer = rasDialer;
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
            await Task.Delay(PollInterval, stoppingToken);

            DateTime now = DateTime.UtcNow;
            bool resumedFromSleep = now - lastTick > SleepGapThreshold;
            lastTick = now;

            if (resumedFromSleep)
                logger.LogInformation("检测到系统从睡眠中恢复。");

            try
            {
                ServiceSettings settings = ServiceSettings.Load();

                if (await rasDialer.GetActiveRasConnectionAsync() is not null)
                {
                    serviceJustStarted = false;
                    continue;
                }

                bool shouldDial =
                    (serviceJustStarted && settings.ReconnectOnStartup) ||
                    (resumedFromSleep && settings.ReconnectOnWake) ||
                    settings.AutoConnect;

                serviceJustStarted = false;

                if (!shouldDial)
                    continue;

                var credentials = LoadCredentials();
                if (credentials is null)
                {
                    logger.LogWarning("未找到凭据，等待下一轮重试。");
                    continue;
                }

                logger.LogInformation("准备拨号：{User}", credentials.Value.User);

                await rasDialer.RasConnectAsync(
                    null,
                    ENTRY_NAME,
                    credentials.Value.User,
                    credentials.Value.Password,
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

    /// <summary>
    /// 读取由 <c>NetSeal.Agent</c> 以 LocalMachine 作用域加密保存的凭据。
    /// </summary>
    private (string User, string Password)? LoadCredentials()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "NetSeal");

        string file = Path.Combine(root, "secrets", "credentials.dat");

        // 兼容旧版本直接放在 NetSeal 根目录的凭据
        if (!File.Exists(file))
        {
            string legacy = Path.Combine(root, "credentials.dat");
            if (File.Exists(legacy))
                file = legacy;
            else
                return null;
        }

        try
        {
            byte[] cipher = File.ReadAllBytes(file);
            byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.LocalMachine);

            using var doc = JsonDocument.Parse(plain);
            string user = doc.RootElement.GetProperty("user").GetString() ?? "";
            string pass = doc.RootElement.GetProperty("password").GetString() ?? "";

            return (user, pass);
        }
        catch (CryptographicException ex)
        {
            logger.LogError(ex, "凭据解密失败，文件可能损坏或 DPAPI 密钥不匹配。");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "读取凭据失败。");
            return null;
        }
    }
}
