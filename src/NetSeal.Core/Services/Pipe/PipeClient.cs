using NetSeal.Core.Models;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NetSeal.Core.Services.Pipe;

public class PipeClient
{

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 发送指令并等待服务端回复。
    /// 使用消息长度前缀协议：4字节长度 + 消息体
    /// </summary>
    /// <param name="requestLine">指令字符串，如 JSON 格式的请求</param>
    /// <returns>服务端返回的响应字符串，失败时返回错误 JSON</returns>
    public async Task<string> SendCommandAsync(string requestLine, CancellationToken ct = default)
    {
        try
        {
            // 创建客户端管道流
            using var clientStream = new NamedPipeClientStream(".", IpcPipeName.NetSealServiceIpcPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // 尝试连接服务端
            await clientStream.ConnectAsync(ct).ConfigureAwait(false);

            // 发送请求：先发长度，再发消息体
            byte[] requestBytes = Encoding.UTF8.GetBytes(requestLine);
            byte[] requestLength = BitConverter.GetBytes(requestBytes.Length);

            await clientStream.WriteAsync(requestLength, 0, 4, ct).ConfigureAwait(false);
            await clientStream.WriteAsync(requestBytes, 0, requestBytes.Length, ct).ConfigureAwait(false);
            await clientStream.FlushAsync(ct).ConfigureAwait(false);

            // 读取响应：先读长度，再读消息体
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ReadTimeout);

            try
            {
                // 读取 4 字节长度头
                byte[] lengthBuffer = new byte[4];
                int bytesRead = await clientStream.ReadAsync(lengthBuffer, 0, 4, timeoutCts.Token).ConfigureAwait(false);

                if (bytesRead == 0)
                    throw new IOException("服务端关闭了管道，未返回响应。");

                if (bytesRead < 4)
                    throw new IOException($"收到不完整的响应长度头，仅读取 {bytesRead} 字节");

                int responseLength = BitConverter.ToInt32(lengthBuffer, 0);

                if (responseLength <= 0 || responseLength > 65536)
                    throw new IOException($"响应长度无效: {responseLength}");

                // 读取响应消息体
                byte[] responseBuffer = new byte[responseLength];
                bytesRead = await clientStream.ReadAsync(responseBuffer, 0, responseLength, timeoutCts.Token).ConfigureAwait(false);

                if (bytesRead < responseLength)
                    throw new IOException($"收到不完整的响应消息，期望 {responseLength} 字节，实际 {bytesRead} 字节");

                return Encoding.UTF8.GetString(responseBuffer);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // 是读超时，不是调用方取消
                throw new TimeoutException($"等待服务端响应超时（{ReadTimeout.TotalSeconds}s）。");
            }
        }
        catch (TimeoutException)
        {
            return JsonSerializer.Serialize(IpcResponse.Fail($"管道通信异常：服务端超时 {ReadTimeout.Seconds} 秒无响应。"));
        }
    }
}