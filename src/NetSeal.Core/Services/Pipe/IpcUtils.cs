using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace NetSeal.Core.Services.Pipe;

public static class IpcUtils
{
    public static IpcResponse ParseResponse(string raw)
    {
        IpcResponse? resp;
        try
        {
            resp = JsonSerializer.Deserialize<IpcResponse>(raw);
        }
        catch (JsonException ex)
        {
            throw new IOException("管道响应格式无效", ex);
        }

        if (resp is null)
            throw new IOException("管道响应为空");

        return resp;
    }
}
