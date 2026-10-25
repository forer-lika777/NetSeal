using NetSeal.Core.Services.Pipe;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace NetSeal.Core.Services.AppSetting;

public class RemoteSettingsStore : ISettingsStore
{
    private readonly PipeClient client;

    public RemoteSettingsStore(PipeClient client)
    {
        this.client = client;
    }

    public async Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        string raw = await client.SendCommandAsync(JsonSerializer.Serialize(new { cmd = "GET", name }), ct);
        var resp = ParseResponse(raw);
        if (!resp.Ok)
            throw new IOException($"读取设置失败: {resp.Error}");
        return resp.Value;
    }

    public async Task SetAsync(string name, string value, CancellationToken ct = default)
    {
        string raw = await client.SendCommandAsync(JsonSerializer.Serialize(new { cmd = "SET", name, value }), ct);
        var resp = ParseResponse(raw);
        if (!resp.Ok)
            throw new IOException($"保存设置失败: {resp.Error}");
    }

    private static PipeResponse ParseResponse(string raw)
    {
        PipeResponse? resp;
        try
        { resp = JsonSerializer.Deserialize<PipeResponse>(raw); }
        catch (JsonException ex) { throw new IOException("管道响应格式无效", ex); }

        if (resp is null)
            throw new IOException("管道响应为空");
        return resp;
    }
}
