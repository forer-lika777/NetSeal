using NetSeal.Core.Models;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;

namespace NetSeal.Core.Services.SystemPowerRegister;

public static class SystemPowerMonitor
{
    private static EventLogWatcher? watcher = null;


    private static readonly object syncLock = new object();
    private static CancellationTokenSource? debounceCts;
    private static PowerMode latestMode = PowerMode.Unknown;
    private static Task? processTask;

    private static readonly TimeSpan cooldown = TimeSpan.FromSeconds(5);

    public static event EventHandler<PowerMode>? PowerModeChanged;

    /// <summary>
    /// <para>注册电源事件。</para>
    /// <para>应用销毁时，调用 <see cref="UnRegister"/> 函数以取消注册事件。</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">当前已注册电源事件</exception>
    public static void Register()
    {
        if (watcher != null)
            throw new InvalidOperationException("已注册电源监听事件。");

        var queryString = "*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] " +
                          "and (EventID=42 or EventID=107 or EventID=506 or EventID=507)]]";

        var query = new EventLogQuery("System", PathType.LogName, queryString);

        watcher = new EventLogWatcher(query);

        watcher.EventRecordWritten += Watcher_EventRecordWritten;

        watcher.Enabled = true;
    }

    private static void Watcher_EventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null)
            return;

        PowerMode mode = e.EventRecord.Id switch
        {
            42 => PowerMode.Suspend,
            506 => PowerMode.ModernStandby,
            107 => PowerMode.ResumeFromSuspend,
            507 => PowerMode.ResumeFromStandby,
            _ => PowerMode.Unknown
        };

        if (mode == PowerMode.Unknown)
            return;

        CancellationToken token;

        lock (syncLock)
        {
            if (mode == latestMode)
                return;

            latestMode = mode;

            debounceCts?.Cancel();
            debounceCts?.Dispose();

            debounceCts = new CancellationTokenSource();
            token = debounceCts.Token;
        }

        processTask = DebounceAndProcessAsync(mode, token);
    }

    private static async Task DebounceAndProcessAsync(PowerMode mode, CancellationToken token)
    {
        try
        {
            await Task.Delay(cooldown, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            PowerModeChanged?.Invoke(null, mode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"触发 PowerModeChanged 事件时出错: {ex}");
        }
    }

    /// <summary>
    /// 取消注册电源事件。
    /// </summary>
    /// <exception cref="InvalidOperationException">当前还未注册电源事件</exception>
    public static void UnRegister()
    {
        if (watcher == null)
            throw new InvalidOperationException("还未注册电源监听事件。");

        // 正确释放资源
        watcher.Enabled = false;
        watcher.Dispose();
        watcher = null;
    }
}
