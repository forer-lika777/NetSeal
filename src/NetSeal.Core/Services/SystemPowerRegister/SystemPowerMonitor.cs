using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static NetSeal.Core.Services.SystemPowerRegister.NativeMethods;

namespace NetSeal.Core.Services.SystemPowerRegister;

public static class SystemPowerMonitor
{
    private static IntPtr? registerHandle = null;

    // 保存委托的静态引用
    private static DeviceNotifyCallbackRoutine callbackDelegate = OnPowerEvent;

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
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerRegisterSuspendResumeNotification(
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
    [DllImport("powrprof.dll", SetLastError = false, ExactSpelling = true)]
    private static extern uint PowerUnregisterSuspendResumeNotification(IntPtr RegistrationHandle);

    public static event EventHandler<PowerMode>? PowerModeChanged;

    /// <summary>
    /// <para>注册电源事件。</para>
    /// <para>应用销毁时，务必调用 <see cref="UnRegister"/> 函数以取消注册事件，否则会引发内存泄露。</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">当前已注册电源事件</exception>
    /// <exception cref="Win32Exception">注册电源通知失败</exception>
    public static void Register()
    {
        if (registerHandle != null)
            throw new InvalidOperationException("当前已注册电源事件。");

        var parameters = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
        {
            Callback = Marshal.GetFunctionPointerForDelegate(callbackDelegate),
            Context = IntPtr.Zero
        };

        uint result = PowerRegisterSuspendResumeNotification(DEVICE_NOTIFY_CALLBACK, ref parameters, out IntPtr handle);

        if (result != 0)
            throw new Win32Exception($"注册电源通知失败，错误码: {result}");

        registerHandle = handle;
    }

    /// <summary>
    /// 取消注册电源事件。
    /// </summary>
    /// <exception cref="InvalidOperationException">当前还未注册电源事件</exception>
    /// <exception cref="Win32Exception">取消注册电源通知失败</exception>
    public static void UnRegister()
    {
        if (registerHandle == null)
            throw new InvalidOperationException("当前还未注册电源事件。");

        uint result = PowerUnregisterSuspendResumeNotification((nint)registerHandle);

        if (result != 0)
            throw new Win32Exception($"取消注册电源通知失败，错误码: {result}");

        registerHandle = null;
    }

    private static uint OnPowerEvent(IntPtr context, uint type, IntPtr setting)
    {
        if (type == (uint)PowerBroadcastType.PBT_APMRESUMEAUTOMATIC)
        {
            PowerModeChanged?.Invoke(context, PowerMode.Resume);
        }
        else if (type == (uint)PowerBroadcastType.PBT_APMSUSPEND)
        {
            PowerModeChanged?.Invoke(context, PowerMode.Suspend);
        }

        return 0;
    }
}
