using NetSeal.Core.Models;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Pipe;
using NetSeal.Core.Services.Ras;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace NetSeal.Service;

public class PipeServer : BackgroundService
{
    private readonly ILogger<PipeServer> logger;
    private readonly IAppSettings appSettings;
    private readonly ISettingsStore settingsStore;
    private readonly ServiceState serviceState;
    private readonly Worker worker;

    // 统一的 JSON 序列化选项配置
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public PipeServer(
        ILogger<PipeServer> logger, 
        IAppSettings appSettings, 
        ISettingsStore settingsStore, 
        ServiceState serviceState,
        Worker worker)
    {
        this.logger = logger;
        this.appSettings = appSettings;
        this.settingsStore = settingsStore;
        this.serviceState = serviceState;
        this.worker = worker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("管道服务已启动，等待连接...");

        PipeSecurity pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));

        RestartAsAdmin();
        // 如果你是 LocalSystem 服务，通常不需要 RestartAsAdmin
        // 如果你需要，请确保它在循环外执行一次

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 注意：这里最好把实例数设大，或者用 -1（无限）
                using var serverStream = NamedPipeServerStreamAcl.Create(
                    IpcPipeName.NetSealServiceIpcPipeName,
                    PipeDirection.InOut,
                    10, // 改大一点，或者 -1
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096,
                    pipeSecurity
                );

                await serverStream.WaitForConnectionAsync(stoppingToken);
                logger.LogDebug("服务已接收到客户端连接");

                // 处理当前连接的请求
                await HandleClientAsync(serverStream, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "管道通信异常");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    // 把处理逻辑抽出来，保持代码清晰
    private async Task HandleClientAsync(NamedPipeServerStream serverStream, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                byte[] lengthBuffer = new byte[4];
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30)); // 客户端 10 秒一次心跳，30 秒没收到就说明死了

                int bytesRead;

                try
                {
                    bytesRead = await serverStream.ReadAsync(lengthBuffer, 0, 4, cts.Token);
                    if (bytesRead == 0)
                        break; // 客户端断开
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning("客户端心跳超时，强制断开");
                    break; // 强行跳出循环，释放实例
                }

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (messageLength <= 0 || messageLength > 65536)
                    continue;

                byte[] messageBuffer = new byte[messageLength];
                bytesRead = await ReadExactAsync(serverStream, messageBuffer, messageLength, stoppingToken);
                if (bytesRead < messageLength)
                    break; // 读不全，断开

                string requestLine = Encoding.UTF8.GetString(messageBuffer);
                string responseLine = await HandleRequestAsync(requestLine, stoppingToken).ConfigureAwait(false);

                byte[] responseBytes = Encoding.UTF8.GetBytes(responseLine);
                byte[] responseLength = BitConverter.GetBytes(responseBytes.Length);

                await serverStream.WriteAsync(responseLength, 0, 4, stoppingToken);
                await serverStream.WriteAsync(responseBytes, 0, responseBytes.Length, stoppingToken);
                await serverStream.FlushAsync(stoppingToken);
            }
        }
        catch (IOException)
        {
            logger.LogDebug("客户端异常断开");
        }
    }

    // 一个强壮的“读满”辅助方法
    private async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken token)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), token);
            if (read == 0)
                break; // 流结束
            totalRead += read;
        }
        return totalRead;
    }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void RestartAsAdmin(string args = "")
    {
        if (IsAdministrator())
            return;

        var processInfo = new ProcessStartInfo
        {
            UseShellExecute = true, // 必须为 true 才能弹 UAC
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = Process.GetCurrentProcess().MainModule?.FileName,
            Verb = "runas", // 关键：触发 UAC
            Arguments = args
        };

        try
        {
            Process.Start(processInfo);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 用户点击了 UAC 的“否”
            logger.LogError("未获取到权限。");
        }

        // 退出当前的非管理员实例
        Environment.Exit(0);
    }

    private async Task<string> HandleRequestAsync(string requestLine, CancellationToken ct)
    {
        IpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<IpcRequest>(requestLine, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "请求格式无效");
            return SerializeError($"请求格式无效: {ex.Message}");
        }

        if (request is null || string.IsNullOrEmpty(request.Cmd))
        {
            logger.LogWarning("请求缺少 cmd");
            return SerializeError("请求缺少 cmd");
        }

        try
        {
            switch (request.Cmd)
            {
                case IpcCommands.CheckServiceStatus:
                    return JsonSerializer.Serialize(IpcResponse.Ok(), JsonOptions);

                case IpcCommands.GetAppSettingsKeyValue:
                    return await HandleGetAsync(request, ct);

                case IpcCommands.SetAppSettingsKeyValue:
                    return await HandleSetAsync(request, ct);

                case IpcCommands.RasConnect:
                    return await HandleConnectRequestAsync(ct);

                case IpcCommands.RasDisconnect:
                    return await HandleDisconnectRequestAsync(ct);

                default:
                    logger.LogWarning("未知命令: {Cmd}", request.Cmd);
                    return SerializeError($"未知命令: {request.Cmd}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "处理命令 {Cmd} 失败", request.Cmd);
            return SerializeError(ex.Message);
        }
    }

    private async Task<string> HandleGetAsync(IpcRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            logger.LogWarning("GET 请求缺少 name 参数");
            return SerializeError("GET 缺少 name");
        }

        string? value = await settingsStore.GetAsync(request.Name, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(new IpcResponse { Success = true, Value = value }, JsonOptions);
    }

    private async Task<string> HandleSetAsync(IpcRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            logger.LogWarning("SET 请求缺少 name 参数");
            return SerializeError("SET 缺少 name");
        }

        if (string.IsNullOrEmpty(request.Value))
        {
            logger.LogWarning("SET 请求缺少 value 参数");
            return SerializeError("SET 缺少 value");
        }

        await settingsStore.SetAsync(request.Name, request.Value, ct).ConfigureAwait(false);

        if (request.Name == appSettings.AutoConnectPolicy.Name)
        {
            logger.LogInformation("自动连接策略已更新，通知 Worker");
            await worker.UpdateAutoConnectPolicyAsync();
        }

        if (request.Name == appSettings.RasCredentials.Name)
        {
            logger.LogInformation("RAS 凭证已更新，通知 Worker");
            await worker.UpdateRasCredentrialsAsync();
        }

        return JsonSerializer.Serialize(new IpcResponse { Success = true }, JsonOptions);
    }

    private async Task<string> HandleConnectRequestAsync(CancellationToken ct)
    {
        try
        {
            logger.LogInformation("收到用户连接请求");
            var response = await worker.ConnectRequest(ct).ConfigureAwait(false);
            logger.LogInformation("连接请求完成，结果: Success={Success}", response.Success);
            return JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "处理连接请求时发生异常");
            return SerializeError($"处理连接请求失败: {ex.Message}");
        }
    }

    private async Task<string> HandleDisconnectRequestAsync(CancellationToken ct)
    {
        try
        {
            try
            {
                await worker.DisconnectRequest(ct).ConfigureAwait(false);
                return JsonSerializer.Serialize(new IpcResponse { Success = true }, JsonOptions);
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(new IpcResponse { Success = false, Error = $"无法识别的输入值。值预期类型是：{typeof(RasConnection)}，错误信息：{ex}" }, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "处理断开连接请求时发生异常");
            return SerializeError($"处理断开连接请求失败: {ex.Message}");
        }
    }

    private static string SerializeError(string error) => JsonSerializer.Serialize(new IpcResponse { Success = false, Error = error }, JsonOptions);
}
