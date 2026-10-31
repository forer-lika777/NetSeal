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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!IsAdministrator())
                {
                    RestartAsAdmin();
                }

                // 创建命名管道
                using var serverStream = NamedPipeServerStreamAcl.Create(
                    IpcPipeName.NetSealServiceIpcPipeName,
                    PipeDirection.InOut,
                    6,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096,
                    pipeSecurity
                );

                try
                {
                    await serverStream.WaitForConnectionAsync(stoppingToken);

                    logger.LogDebug("服务已接收到客户端连接");

                    // 一个连接处理多条请求，直到客户端断开
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        // 读取消息长度（4字节）
                        byte[] lengthBuffer = new byte[4];
                        int bytesRead = await serverStream.ReadAsync(lengthBuffer, 0, 4, stoppingToken).ConfigureAwait(false);

                        if (bytesRead == 0)
                        {
                            logger.LogDebug("客户端已断开连接");
                            break;   // 客户端断开
                        }

                        if (bytesRead < 4)
                        {
                            logger.LogWarning("收到不完整的消息长度头，仅读取 {BytesRead} 字节", bytesRead);
                            continue;
                        }

                        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                        if (messageLength <= 0 || messageLength > 65536)  // 最多64KB消息
                        {
                            logger.LogWarning("消息长度无效: {Length}", messageLength);
                            continue;
                        }

                        // 读取消息体
                        byte[] messageBuffer = new byte[messageLength];
                        bytesRead = await serverStream.ReadAsync(messageBuffer, 0, messageLength, stoppingToken).ConfigureAwait(false);

                        if (bytesRead < messageLength)
                        {
                            logger.LogWarning("收到不完整的消息体，期望 {Expected} 字节，实际 {Actual} 字节", messageLength, bytesRead);
                            continue;
                        }

                        string requestLine = Encoding.UTF8.GetString(messageBuffer);
                        string responseLine = await HandleRequestAsync(requestLine, stoppingToken).ConfigureAwait(false);

                        // 发送响应：先发长度，再发消息体
                        byte[] responseBytes = Encoding.UTF8.GetBytes(responseLine);
                        byte[] responseLength = BitConverter.GetBytes(responseBytes.Length);

                        await serverStream.WriteAsync(responseLength, 0, 4, stoppingToken).ConfigureAwait(false);
                        await serverStream.WriteAsync(responseBytes, 0, responseBytes.Length, stoppingToken).ConfigureAwait(false);
                        await serverStream.FlushAsync(stoppingToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "管道连接处理异常");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "管道通信异常");
                // 稍微等一下，防止死循环疯狂报错
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("管道服务已停止");
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
