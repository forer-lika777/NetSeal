using NetSeal.Core.Models;
using System.Diagnostics.Eventing.Reader;

namespace NetSeal.Core.Services.SystemPowerRegister;

public static class SystemPowerMonitor
{
    private static EventLogWatcher? watcher = null;

    private static readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private static DateTime lastUpdateTime = DateTime.MinValue;
    private static readonly Lock locker = new();

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
        lock (locker)
        {
            var now = DateTime.UtcNow;
            if (now - lastUpdateTime < cooldown)
                return;

            lastUpdateTime = now;
        }

        if (e.EventRecord == null)
            return;

        using (e.EventRecord)
        {
            switch (e.EventRecord.Id)
            {
                case 42:
                    PowerModeChanged?.Invoke(sender, PowerMode.Suspend);
                    break;
                case 107:
                    PowerModeChanged?.Invoke(sender, PowerMode.Resume);
                    break;
                case 506:
                    PowerModeChanged?.Invoke(sender, PowerMode.ModernStandby);
                    break;
                case 507:
                    PowerModeChanged?.Invoke(sender, PowerMode.Resume);
                    break;
            }
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
