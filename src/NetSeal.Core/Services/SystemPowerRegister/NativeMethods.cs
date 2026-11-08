using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NetSeal.Core.Services.SystemPowerRegister;

internal class NativeMethods
{
    public enum PowerBroadcastType
    {
        /// <summary>
        /// Power status has changed.
        /// </summary>
        PBT_APMPOWERSTATUSCHANGE = 10,
        /// <summary>
        /// Operation is resuming automatically from a low-power state. 
        /// This message is sent every time the system resumes.
        /// </summary>
        PBT_APMRESUMEAUTOMATIC = 18,
        /// <summary>
        /// Operation is resuming from a low-power state. 
        /// This message is sent after PBT_APMRESUMEAUTOMATIC if the resume is triggered by user input, 
        /// such as pressing a key.
        /// </summary>
        PBT_APMRESUMESUSPEND = 7,
        /// <summary>
        /// System is suspending operation.
        /// </summary>
        PBT_APMSUSPEND = 4,
        /// <summary>
        /// A power setting change event has been received.
        /// </summary>
        PBT_POWERSETTINGCHANGE = 32787,
    }

    public const uint DEVICE_NOTIFY_CALLBACK = 2;

    /// <summary>
    /// Contains parameters used when registering for a power notification.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
    {
        /// <summary>
        /// Indicates the callback function that will be called when the application receives the notification.
        /// </summary>
        public IntPtr Callback;
        /// <summary>
        /// The context of the application registering for the notification.
        /// </summary>
        public IntPtr Context;
    }

    /// <summary>
    /// An application's DeviceNotifyCallbackRoutine callback function is used for receiving power notifications.
    /// </summary>
    /// <param name="context">The context provided when registering for the power notification.</param>
    /// <param name="type">The type of power event that caused this notification.</param>
    /// <param name="setting">The value of this parameter depends on the type of notification subscribed to.</param>
    /// <returns></returns>
    public delegate uint DeviceNotifyCallbackRoutine(IntPtr context, uint type, IntPtr setting);
}
