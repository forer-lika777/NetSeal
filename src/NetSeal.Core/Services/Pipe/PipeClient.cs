using NetSeal.Core.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NetSeal.Core.Services.Pipe;

public class PipeClient
{

    // 连接超时时间（毫秒）
    private const int ConnectTimeoutMs = 1000;

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 发送指令并等待服务端回复。
    /// </summary>
    /// <param name="requestLine">指令字符串，如 "SAVE_SETTING|Key|Value"</param>
    /// <returns>服务端返回的字符串，如 "OK"；如果连接失败或异常，返回 "ERROR"</returns>
    public async Task<string> SendCommandAsync(string requestLine, CancellationToken ct = default)
    {
        try
        {
            // 创建客户端管道流
            using var clientStream = new NamedPipeClientStream(".", IpcPipeName.NetSealServiceIpcPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // 尝试连接服务端
            await clientStream.ConnectAsync(ConnectTimeoutMs, ct).ConfigureAwait(false);

            using var writer = new StreamWriter(clientStream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            // 发送指令
            await writer.WriteLineAsync(requestLine.AsMemory(), ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ReadTimeout);

            // 等待服务端回复
            string? response;
            try
            {
                using var reader = new StreamReader(clientStream, Encoding.UTF8, leaveOpen: true);
                response = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // 是读超时，不是调用方取消
                throw new TimeoutException($"等待服务端响应超时（{ReadTimeout.TotalSeconds}s）。");
            }

            if (response is null)
                throw new IOException("服务端关闭了管道，未返回响应。");

            return response;
        }
        catch (TimeoutException)
        {
            Debug.WriteLine("管道连接超时：NetSeal 服务可能未运行。");
            return JsonSerializer.Serialize(IpcResponse.Fail("NetSeal 服务可能未运行（连接超时）。"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"管道通信异常: {ex.Message}");
            return JsonSerializer.Serialize(IpcResponse.Fail($"管道通信异常：{ex.Message}"));
        }
    }
}