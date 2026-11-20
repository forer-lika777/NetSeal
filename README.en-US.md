[English](README.en-US.md) | [简体中文](README.md)

# Reimu's NetSeal

Reimu's NetSeal is a Windows desktop application for automatic network dial-up connections.

Reimu's NetSeal is an experimental project, and is currently still in the development phase.

## Core Features

* **Connect Before Login**: Built on a Windows Service architecture, it can establish network dial-up connections at boot time (before the user logs in).
* **Targeted Network Recognition**: Identifies and matches network environments, only initiating a dial-up connection when connected to the specified network.
* **Advanced Connection Management**: Supports automatic reconnection upon waking from sleep, and reconnects automatically if the connection unexpectedly drops.

## Tech Stack

* **Windows App SDK (WinUI 3)**: A modern Windows desktop application framework based on Fluent UI. In this project, NetSeal.WinUI uses Windows SDK self-contained to support running in unpackaged mode.
* **Windows Service**: Enables auto-start at boot and persistent background operation. In this project, NetSeal.Service enables Native AOT compilation to run without requiring the .NET Runtime.
* **Windows API / COM Interop**: Used for network state recognition and dial-up connection management. In this project, NetSeal.Core uses CsWin32 P/Invoke source generator to call the Windows API.
* **C#**: The entire project is written in C#. Its framework version is .NET 10.

## Platform Availability

The application can only run on Windows 10 (version 1809 and later) or higher.

## Additional Information

In the Reimu's NetSeal project, approximately 6~8% of the actual executable code is AI-assisted.

## See Also

* Ancient Legacy -> [Ras.h header - Win32 apps | Microsoft Learn](https://learn.microsoft.com/windows/win32/api/ras/)
* WinUI 3 -> [WinUI 3 - Windows apps | Microsoft Learn](https://learn.microsoft.com/windows/apps/winui/winui3/)
* Modern Standby in Windows -> [Networking power management for Modern Standby platforms | Microsoft Learn](https://learn.microsoft.com/windows-hardware/design/device-experiences/networking-power-management-for-modern-standby-platforms)