[简体中文](README.md) | English

# Reimu's NetSeal

Reimu's NetSeal is a Windows desktop application for automatic network dial-up connections.

Reimu's NetSeal is an experimental project, and is currently still in the development phase.

## Core Features of Reimu's NetSeal

* **Connect Before Login**: Built on a Windows Service architecture, it can establish network dial-up connections at boot time (before the user logs in).
* **Targeted Network Recognition**: Identifies and matches network environments, only initiating a dial-up connection when connected to the specified network.
* **Advanced Connection Management**: Supports automatic reconnection upon waking from sleep or modern standby, and reconnects automatically if the connection unexpectedly drops.

## Tech Stack of Reimu's NetSeal

* **Windows App SDK (WinUI 3)**: A modern Windows desktop application framework based on Fluent UI. In this project, NetSeal.WinUI uses Windows SDK self-contained to support running in unpackaged mode.
* **Windows Service**: Enables auto-start at boot and persistent background operation. In this project, NetSeal.Service enables Native AOT compilation to run without requiring the .NET Runtime.
* **Windows API / COM Interop**: Used for network state recognition and dial-up connection management. In this project, NetSeal.Core uses CsWin32 P/Invoke source generator to call the Windows API.
* **C#**: The project is written in C#. Its framework version is .NET 10.

## Comparing to the Task Scheduler, what advantages does Reimu's NetSeal have?

* **Friendly user interface.** The app is ready to use right after installation; advanced connection operations can be managed from a convenient UI window, with no need to set up a cumbersome Task Scheduler.
* **Highly controllable state-transition behavior.** For various specific situations, the service is highly controllable, with targeted code logic that can handle a wide range of complex environments.

  Since Windows 10, Windows has introduced Modern Standby (S0). Different from traditional sleep (S3), the CPU keeps running during Modern Standby, allowing the system to wake more quickly. Modern Standby (S0) usually occurs on laptops when the screen turns off or the lid is closed—although these actions are designated as "Sleep" in the settings, as it does not refer to the tranditional sleep (S3).

  Ethernet will be diabled during modern standby. For a PPPoE connection, it means that the connection needs to be dropped. The service needs to stay silent during sleep and Modern Standby, and on wake, check the network state and perform the connection. However, to do this, many problems will arise:

  1. **Subscribing to traditional sleep events does not receive Modern Standby state-transition events.** The service needs to both register for sleep messages and Windows log messages and listen for Modern Standby state-transition events.

  2. **Modern Standby behavior may be uncontrollable.** After entering standby, Windows frequently exhibits so-called "false wake". For certain reasons, the system wakes on its own and then instantly returns to standby. The service needs to implement specific debounce logic for such behavior.

  3. **Sleep and Modern Standby behavior may be uncontrollable.** When clicking Sleep from the Start menu, a Modern Standby enter notification may also be triggered, and even after that, a Modern Standby exit notification may be triggered, and the system is actually entering sleep. The service needs to implement specific handling logic for such behavior.

  4. **Dial-up behavior during standby may be uncontrollable.** During Modern Standby, the Ethernet device is disabled; if a dial-up connection is attempted at this time, the system may enter a "false connection" state. After waking, even if the system shows Ethernet network is connected, it may still actually unable to access to the Internet. This is based on testing in real environments. The service needs to precisely judge device state and handle the dial-up connection.

  5. **Expected shutdown behavior may be uncontrollable.** As a long-term background resident program, the service receives a sleep (suspend) notification at shutdown and a wake (resume) notification at boot. But usually no specific handling is needed for this.

## Platform Availability

The application can only run on Windows 10 (version 1809 and later) or higher.

## Additional Information

In the Reimu's NetSeal project, approximately 6~8% of the actual executable code is AI-assisted. No Vibe-Coding code is used in this project.

## See Also

* Ancient Legacy -> [Ras.h header - Win32 apps | Microsoft Learn](https://learn.microsoft.com/windows/win32/api/ras/)
* WinUI 3 -> [WinUI 3 - Windows apps | Microsoft Learn](https://learn.microsoft.com/windows/apps/winui/winui3/)
* Modern Standby in Windows -> [Networking power management for Modern Standby platforms | Microsoft Learn](https://learn.microsoft.com/windows-hardware/design/device-experiences/networking-power-management-for-modern-standby-platforms)