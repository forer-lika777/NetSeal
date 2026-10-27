using NetSeal.Core.Models;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Pipe;
using NetSeal.Core.Services.Ras;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NetSeal.Service;

public class PipeServer : BackgroundService
{
    private readonly ILogger<PipeServer> logger;
    private readonly IAppSettings appSettings;
    private readonly ISettingsStore settingsStore;

    public PipeServer(ILogger<PipeServer> logger, IAppSettings appSettings, ISettingsStore settingsStore)
    {
        this.logger = logger;
        this.appSettings = appSettings;
        this.settingsStore = settingsStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("管道服务已启动，等待连接...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 创建命名管道
                using var serverStream = new NamedPipeServerStream(
                    IpcPipeName.NetSealServiceIpcPipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                try
                {
                    await serverStream.WaitForConnectionAsync(stoppingToken);

                    logger.LogDebug("服务已接收到客户端连接");

                    using var reader = new StreamReader(serverStream, Encoding.UTF8, leaveOpen: true);

                    // 一个连接处理多条请求，直到客户端断开
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync(stoppingToken).ConfigureAwait(false);
                        if (line is null)
                            break;   // 客户端断开

                        string responseLine = await HandleRequestAsync(line, stoppingToken).ConfigureAwait(false);
                        using var writer = new StreamWriter(serverStream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                        await writer.WriteLineAsync(responseLine.AsMemory(), stoppingToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"管道连接处理异常: {ex.Message}");
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
    }

    private async Task<string> HandleRequestAsync(string requestLine, CancellationToken ct)
    {
        IpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<IpcRequest>(requestLine);
        }
        catch (JsonException ex)
        {
            return SerializeError($"请求格式无效: {ex.Message}");
        }

        if (request is null || string.IsNullOrEmpty(request.Cmd))
            return SerializeError("请求缺少 cmd");

        try
        {
            switch (request.Cmd)
            {
                case IpcCommands.GetAppSettingsKeyValue:
                    {
                        if (string.IsNullOrEmpty(request.Name))
                            return SerializeError("GET 缺少 name");

                        string? value = await settingsStore.GetAsync(request.Name, ct).ConfigureAwait(false);
                        return JsonSerializer.Serialize(new IpcResponse { Success = true, Value = value });
                    }

                case IpcCommands.SetAppSettingsKeyValue:
                    {
                        if (string.IsNullOrEmpty(request.Name))
                            return SerializeError("SET 缺少 name");
                        if (request.Value is null)
                            return SerializeError("SET 缺少 value");

                        await settingsStore.SetAsync(request.Name, request.Value, ct).ConfigureAwait(false);
                        return JsonSerializer.Serialize(new IpcResponse { Success = true });
                    }

                default:
                    return SerializeError($"未知命令: {request.Cmd}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理 {request.Cmd} 失败: {ex.Message}");
            return SerializeError(ex.Message);
        }
    }
    private static string SerializeError(string error) => JsonSerializer.Serialize(new IpcResponse { Success = false, Error = error });
}
