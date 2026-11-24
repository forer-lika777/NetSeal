简体中文 | [English](README.en-US.md)

Reimu's NetSeal
===

Reimu's NetSeal 是一个自动拨号连接 PPPoE 网络的 Windows 桌面应用。

Reimu's NetSeal 是一个实验性项目，目前仍处于开发阶段。

Reimu's NetSeal 的核心功能
---

* 开机登录前连接：基于 Windows 服务的体系，在开机时（账户登录前）即可完成网络拨号连接。
* 专一网络识别：识别匹配网络环境，仅连接到指定网络时才进行拨号连接。
* 高级连接管理：支持从睡眠和新式待机中苏醒时自动连接、连接意外断开时重新连接。

Reimu's NetSeal 的技术栈
---

* Windows App SDK (WinUI 3)：基于 Fluent UI 的现代化 Windows 前端桌面应用框架。在此项目中，NetSeal.WinUI 使用了 Windows SDK 自包含来支持非打包应用的运行。
* Windows 服务：实现开机自启动、后台持久化运行。在此项目中，NetSeal.Service 使用了 Native AOT 来支持无需依赖 .NET 运行时的运行。
* Windows API / COM 互操作：实现网络状态识别和拨号连接管理功能。在此项目中，NetSeal.Core 使用了 CsWin32 PInvoke 源生成器来实现部分 Windows API 的调用。
* C#：项目的所有部分使用 C# 编写。框架版本是 .NET 10。

相较于普通任务计划程序，Reimu's NetSeal 有何优势？
---

* 用户界面友好。应用安装后开箱即用，从方便的 UI 界面中即可管理高级连接操作，无需设置繁琐的任务计划程序。
* 断开重连功能。后台常驻能持续监测连接状态，并在连接意外断开时为您重新连接。
* 状态管理高度可控。对于各种特定状况，服务高度可控，具有针对性的代码逻辑能应对多种复杂环境。

  若您的设备支持新式待机，Reimu's NetSeal 能相对而言更好地管理您的拨号网络连接。自 Windows 10 以来，Windows 引入了新式待机（S0）。不同于传统睡眠（S3），新式待机期间 CPU 仍在运行，能迅速从待机状态唤醒。新式待机（S0）通常发生在笔记本设备息屏、合上盖子时，即使在设置中这些操作被指定为 “睡眠”，因为他指的并不是传统睡眠（S3）。

  新式待机期间，以太网设备协议会被卸载。对于 PPPoE 拨号网络而言，以太网设备协议卸载意味着连接需要断开。服务应当在睡眠、新式待机时静默，在唤醒时检查网络状态并执行连接。但就这一步而言，会出现许多问题：

  1. 订阅传统睡眠的事件并不接收新式待机状态切换事件。服务需要同时注册睡眠消息和 Windows 日志消息并监听新式待机状态切换事件。

  2. 新式待机的行为可能不可控。Windows 在进入待机状态后会经常出现所谓 “假醒” 行为。出于某些原因，系统会自行醒来随后瞬间回到待机状态。服务需要为其制定特定的防抖逻辑。

  3. 睡眠和新式待机的行为可能不可控。从开始菜单点击进入睡眠状态时，可能也会触发进入新式待机通知，甚至在这之后再次触发退出新式待机通知，但此时系统真正进入的是睡眠状态。服务需要为其制定特定的处理逻辑。

  4. 待机期间拨号的行为可能不符合预期。新式待机期间以太网设备被禁用，若此时进行拨号连接，系统可能进入 “假连接” 状态。唤醒之后，即使系统显示已连接到互联网，可能仍处于断开连接状态。这是根据实际环境测试得到的。服务需要精确判断设备状态，处理拨号连接。

  5. 关机的行为可能不符合预期。服务作为长期后台驻留程序，关机时接收睡眠（挂起）通知，开机时接收唤醒（恢复）通知。但通常不需要对此进行特定处理。

平台可用性
---

应用仅可在 Windows 10 （1809 及以后）或更高版本的 Windows 系统中运行。

其他相关信息
---

Reimu's NetSeal 项目中，约 6~8％ 的实际可执行代码由 AI 辅助生成。

另请参阅
---

* 上古老坟 -> [Ras.h 标头 - Win32 应用 | Microsoft Learn](https://learn.microsoft.com/windows/win32/api/ras/)
* WinUI 3 -> [WinUI 3 - Windows 应用开发 | Microsoft Learn](https://learn.microsoft.com/windows/apps/winui/winui3/)
* Windows 中的新式待机 -> [新式待机平台的网络电源管理 | Microsoft Learn](https://learn.microsoft.com/windows-hardware/design/device-experiences/networking-power-management-for-modern-standby-platforms)