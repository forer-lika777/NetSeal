using NetSeal.Core.Models;
using NetSeal.Core.Services.Pipe;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;

namespace NetSeal.Core.Services.AppSetting;

public class RemoteSettingsStore : ISettingsStore
{
    private readonly PipeClient client;

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);

    public RemoteSettingsStore(PipeClient client)
    {
        this.client = client;
    }

    public async Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        using var timeoutCts = new CancellationTokenSource(ReadTimeout);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        string request = JsonSerializer.Serialize(new IpcRequest() { Cmd = IpcCommands.GetAppSettingsKeyValue, Name = name });

        string raw = await client.SendCommandAsync(request, cts.Token);

        var resp = IpcUtils.ParseResponse(raw);
        if (!resp.Success)
            throw new IOException($"读取设置失败: {resp.Error}");

        return resp.Value;
    }

    public async Task SetAsync(string name, string value, CancellationToken ct = default)
    {
        using var timeoutCts = new CancellationTokenSource(ReadTimeout);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        string request = JsonSerializer.Serialize(new IpcRequest() { Cmd = IpcCommands.SetAppSettingsKeyValue, Name = name, Value = value });

        string raw = await client.SendCommandAsync(request, cts.Token);

        var resp = IpcUtils.ParseResponse(raw);
        if (!resp.Success)
            throw new IOException($"保存设置失败: {resp.Error}");
    }
}
