using NetSeal.Core.Models;
using NetSeal.Core.Services.AppSetting;
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
    private readonly INetworkSettings appSettings;
    private readonly ISettingsStore settingsStore;
    private readonly ServiceState serviceState;
    private readonly Worker worker;

    public PipeServer(
        ILogger<PipeServer> logger,
        INetworkSettings appSettings,
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

        if (!IsAdministrator())
            RestartAsAdmin();

        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream? serverStream = null;
            try
            {
                serverStream = NamedPipeServerStreamAcl.Create(
                    IpcPipeName.NetSealServiceIpcPipeName,
                    PipeDirection.InOut,
                    10,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096,
                    pipeSecurity
                );

                await serverStream.WaitForConnectionAsync(stoppingToken);
                logger.LogDebug("服务已接收到客户端连接");

                var currentStream = serverStream;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleClientAsync(currentStream, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "处理客户端请求时异常");
                    }
                    finally
                    {
                        // 任务结束时，释放流
                        currentStream.Dispose();
                    }
                }, stoppingToken);

                serverStream = null;
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
            finally
            {
                if (serverStream != null)
                {
                    await serverStream.DisposeAsync();
                }
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream serverStream, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                byte[] lengthBuffer = new byte[4];
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                int bytesRead;

                try
                {
                    bytesRead = await serverStream.ReadAsync(lengthBuffer, 0, 4, cts.Token);
                    if (bytesRead == 0)
                    {
                        logger.LogDebug("客户端连接已断开");
                        break;
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning("客户端心跳超时，已强制断开");
                    break;
                }

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (messageLength <= 0 || messageLength > 65536)
                    continue;

                byte[] messageBuffer = new byte[messageLength];
                bytesRead = await ReadExactAsync(serverStream, messageBuffer, messageLength, stoppingToken);
                if (bytesRead < messageLength)
                    break;

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
            logger.LogWarning("客户端连接异常断开");
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
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = Process.GetCurrentProcess().MainModule?.FileName,
            Verb = "runas",
            Arguments = args
        };

        try
        {
            Process.Start(processInfo);
            Environment.Exit(0);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            logger.LogWarning("未获取到权限（以非管理员模式运行程序会导致某些操作无法执行）。");
        }
    }

    private async Task<string> HandleRequestAsync(string requestLine, CancellationToken ct)
    {
        IpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<IpcRequest>(requestLine, IpcRequestJsonContext.Default.IpcRequest);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "请求格式无效");
            return IpcResponse.SerializeFail($"请求格式无效: {ex.Message}");
        }

        if (request is null || string.IsNullOrEmpty(request.Cmd))
        {
            logger.LogWarning("请求缺少 cmd");
            return IpcResponse.SerializeFail("请求缺少 cmd");
        }

        try
        {
            switch (request.Cmd)
            {
                case IpcCommands.CheckServiceStatus:
                    logger.LogDebug("已接收到客户端心跳请求。");
                    return IpcResponse.SerializeOk();

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
                    return IpcResponse.SerializeFail($"未知命令: {request.Cmd}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "处理命令 {Cmd} 失败", request.Cmd);
            return IpcResponse.SerializeFail(ex.Message);
        }
    }

    private async Task<string> HandleGetAsync(IpcRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            logger.LogWarning("GET 请求缺少 name 参数");
            return IpcResponse.SerializeFail("GET 缺少 name");
        }

        string? value = await settingsStore.GetAsync(request.Name, ct).ConfigureAwait(false);
        return IpcResponse.SerializeOk(value);
    }

    private async Task<string> HandleSetAsync(IpcRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            logger.LogError("SET 请求缺少 name 参数");
            return IpcResponse.SerializeFail("SET 缺少 name");
        }

        if (string.IsNullOrEmpty(request.Value))
        {
            logger.LogError("SET 请求缺少 value 参数");
            return IpcResponse.SerializeFail("SET 缺少 value");
        }

        await settingsStore.SetAsync(request.Name, request.Value, ct).ConfigureAwait(false);

        if (request.Name == appSettings.AutoConnectPolicy.Name)
        {
            logger.LogInformation("自动连接策略已更新，通知 Worker");
            await worker.UpdateAutoConnectPolicyAsync();
        }
        else if (request.Name == appSettings.RasCredentials.Name)
        {
            logger.LogInformation("RAS 凭证已更新，通知 Worker");
            await worker.UpdateRasCredentrialsAsync();
        }
        else if (request.Name == appSettings.SelectedNetwork.Name)
        {
            logger.LogInformation("已选网络已更新，通知 Worker");
            await worker.UpdateSelectedNetworkAsync();
        }

        return IpcResponse.SerializeOk();
    }

    private async Task<string> HandleConnectRequestAsync(CancellationToken ct)
    {
        try
        {
            logger.LogInformation("收到用户连接请求");
            var response = await worker.ConnectRequest(ct).ConfigureAwait(false);
            logger.LogInformation("连接请求完成，结果: Success={Success}", response.Success);
            return IpcResponse.SerializeOk();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "处理连接请求时发生异常");
            return IpcResponse.SerializeFail($"处理连接请求失败: {ex.Message}");
        }
    }

    private async Task<string> HandleDisconnectRequestAsync(CancellationToken ct)
    {
        try
        {
            try
            {
                await worker.DisconnectRequest(ct).ConfigureAwait(false);
                return IpcResponse.SerializeOk();
            }
            catch (JsonException ex)
            {
                return IpcResponse.SerializeFail($"无法识别的输入值。值预期类型是：{typeof(RasConnection)}，错误信息：{ex}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "处理断开连接请求时发生异常");
            return IpcResponse.SerializeFail($"处理断开连接请求失败: {ex.Message}");
        }
    }
}
