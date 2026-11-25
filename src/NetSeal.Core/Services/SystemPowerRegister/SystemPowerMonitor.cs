using Microsoft.Extensions.Logging;
using NetSeal.Core.Models;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using static NetSeal.Core.Services.SystemPowerRegister.NativeMethods;

namespace NetSeal.Core.Services.SystemPowerRegister;

public partial class SystemPowerMonitor
{
    private readonly ILogger<SystemPowerMonitor> logger;
    private IntPtr? registerHandle = null;
    private EventLogWatcher? watcher = null;

    private readonly Lock syncLock = new Lock();
    private CancellationTokenSource? debounceCts = null;
    // 服务初始化时系统一定处于非待机状态
    private PowerMode latestMode = PowerMode.ResumeFromStandby;

    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(1);

    // 保存委托的静态引用
    private readonly DeviceNotifyCallbackRoutine callbackDelegate;

    public event EventHandler<PowerMode>? PowerModeChanged;

    public SystemPowerMonitor(ILogger<SystemPowerMonitor> logger)
    {
        this.logger = logger;
        callbackDelegate = OnPowerEvent;
    }

    /// <summary>
    /// <para>注册电源事件。</para>
    /// <para>应用销毁时，调用 <see cref="UnRegister"/> 函数以取消注册事件。</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">当前已注册电源事件</exception>
    public void Register()
    {
        if (watcher != null || registerHandle != null)
            throw new InvalidOperationException("已注册电源监听事件。");

        // Register suspend
        var parameters = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
        {
            Callback = Marshal.GetFunctionPointerForDelegate(callbackDelegate),
            Context = IntPtr.Zero
        };

        uint result = PowerRegisterSuspendResumeNotification(DEVICE_NOTIFY_CALLBACK, ref parameters, out IntPtr handle);

        if (result != 0)
            throw new Win32Exception($"注册电源通知失败，错误码: {result}");

        registerHandle = handle;

        // Register log watcher (modern standby)
        var queryString = "*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] " +
                          "and (EventID=506 or EventID=507)]]";

        var query = new EventLogQuery("System", PathType.LogName, queryString);

        watcher = new EventLogWatcher(query);

        watcher.EventRecordWritten += Watcher_EventRecordWritten;

        watcher.Enabled = true;
    }

    /// <summary>
    /// 取消注册电源事件。
    /// </summary>
    /// <exception cref="InvalidOperationException">当前还未注册电源事件</exception>
    public void UnRegister()
    {
        if (watcher == null || registerHandle == null)
            throw new InvalidOperationException("还未注册电源监听事件。");

        // Unregister suspend
        uint result = PowerUnregisterSuspendResumeNotification((nint)registerHandle);

        if (result != 0)
            throw new Win32Exception($"取消注册电源通知失败，错误码: {result}");

        registerHandle = null;

        // Unregister log watcher
        watcher.Enabled = false;
        watcher.Dispose();
        watcher = null;
    }

    private uint OnPowerEvent(IntPtr context, uint type, IntPtr setting)
    {
        PowerMode mode = PowerMode.Unknown;

        if (type == (uint)PowerBroadcastType.PBT_APMRESUMEAUTOMATIC)
        {
            mode = PowerMode.ResumeFromSuspend;
        }
        else if (type == (uint)PowerBroadcastType.PBT_APMSUSPEND)
        {
            mode = PowerMode.Suspend;
        }

        if (mode == PowerMode.Unknown)
            return 0;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("收到了来自应用程序广播的变更事件。模式：{mode}", mode);

        Invoke(mode);

        return 0;
    }

    // 此处不使用 CsWin32 生成器的原因是，CsWin32 生成的签名是针对窗口应用程序设计的
    // 生成的针对服务程序的签名需要管理指针，手动引入相对而言更加方便

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/powerbase/nf-powerbase-powerregistersuspendresumenotification">PowerRegisterSuspendResumeNotification</see></b> 函数注册以在系统暂停或恢复时接收通知。
    /// </summary>
    /// <param name="flags">
    /// 此参数必须 DEVICE_NOTIFY_CALLBACK。
    /// </param>
    /// <param name="recipient">
    /// 此参数是指向 <see cref="DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS"/> 结构的指针。
    /// 在这种情况下，回调函数为 <see cref="DeviceNotifyCallbackRoutine"/>。
    /// 执行 Callback 函数时，将设置 Type 参数，指示发生的事件类型。
    /// 可能的值包括 PBT_APMSUSPEND、PBT_APMRESUMESUSPEND和 PBT_APMRESUMEAUTOMATIC - 有关详细信息，请参阅 <see href="https://learn.microsoft.com/zh-cn/windows/win32/power/power-management-events">电源管理事件</see>。
    /// Setting 参数不用于暂停/恢复通知。
    /// </param>
    /// <param name="registrationHandle">
    /// 注册的句柄。 使用此句柄取消注册通知。
    /// </param>
    /// <returns>
    /// 如果调用成功，则返回ERROR_SUCCESS (0); 如果调用失败，则返回非零值。
    /// </returns>
    [LibraryImport("powrprof.dll", SetLastError = true)]
    private static partial uint PowerRegisterSuspendResumeNotification(
        uint flags,
        ref DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient,
        out IntPtr registrationHandle);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/powerbase/nf-powerbase-powerunregistersuspendresumenotification">PowerUnregisterSuspendResumeNotification</see></b> 函数取消注册以在系统暂停或恢复时接收通知。
    /// </summary>
    /// <param name="RegistrationHandle">
    /// 通过调用 <see cref="PowerRegisterSuspendResumeNotification(uint, ref DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS, out nint)"/> 函数获取的注册句柄。
    /// </param>
    /// <returns>
    /// 如果调用成功，则返回ERROR_SUCCESS (0); 如果调用失败，则返回非零值。
    /// </returns>
    [LibraryImport("powrprof.dll", SetLastError = false)]
    private static partial uint PowerUnregisterSuspendResumeNotification(
        IntPtr RegistrationHandle);

    private void Watcher_EventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null)
            return;

        PowerMode mode = e.EventRecord.Id switch
        {
            506 => PowerMode.ModernStandby,
            507 => PowerMode.ResumeFromStandby,
            _ => PowerMode.Unknown
        };

        if (mode == PowerMode.Unknown)
            return;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("收到了来自日志记录的变更事件。模式：{mode}。信息：{description}", mode, e.EventRecord.FormatDescription());

        Invoke(mode);
    }

    private DateTime lastResumeTime = DateTime.MinValue; // 记录最后一次醒来的时间

    private void Invoke(PowerMode mode)
    {
        // 阅读项目文档以了解为何需要在此处设计复杂的针对性防抖逻辑
        CancellationToken token;

        lock (syncLock)
        {
            if (IsResume(mode) == false && (DateTime.Now - lastResumeTime).TotalSeconds < 5)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("此睡眠事件自上次恢复以来小于阈值 5 秒，此事件将被忽略。模式：{mode}", mode);

                return;
            }

            debounceCts?.Cancel();
            debounceCts?.Dispose();
            debounceCts = null;

            if (mode == latestMode || IsResume(mode) == IsResume(latestMode))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("触发状态与上一次记录时相同（或他们是同一类型的状态）。此线程已退出。模式：{mode}", mode);

                return;
            }

            if (IsResume(mode) == true)
            {
                lastResumeTime = DateTime.Now;
            }

            debounceCts = new CancellationTokenSource();
            token = debounceCts.Token;
        }

        _ = DebounceAndProcessAsync(mode, token);
    }

    private async Task DebounceAndProcessAsync(PowerMode mode, CancellationToken token)
    {
        if (IsResume(mode))
        {
            try
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("模式为 {mode} 的线程正在等待冷却...", mode);

                await Task.Delay(cooldown, token);
            }
            catch (OperationCanceledException)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("冷却期间内收到了新的变更事件。此线程已退出。模式：{mode}", mode);

                return;
            }
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("系统即将睡眠，立即触发事件。模式：{mode}", mode);
        }

        latestMode = mode;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("触发模式为 {mode} 的变更事件", mode);

        try
        {
            PowerModeChanged?.Invoke(null, mode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "触发 PowerModeChanged 事件时出错。");
        }
    }

    private static bool IsResume(PowerMode mode)
    {
        if (mode == PowerMode.ResumeFromStandby || mode == PowerMode.ResumeFromSuspend)
            return true;

        if (mode == PowerMode.Suspend || mode == PowerMode.ModernStandby)
            return false;

        return false;
    }
}
