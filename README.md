Reimu's NetSeal
===

Reimu's NetSeal 是一个自动网络拨号连接的 Windows 桌面应用。

Reimu's NetSeal 是一个实验性项目，目前仍处于开发阶段。

Reimu's NetSeal 的核心功能
---

* 开机登录前连接：基于 Windows 服务的体系，在开机时（账户登录前）即可完成网络拨号连接。
* 专一网络识别：识别匹配网络环境，仅连接到指定网络时才进行拨号连接。
* 高级连接管理：支持从睡眠中苏醒时自动连接、连接意外断开时重新连接。

Reimu's NetSeal 的技术栈
---

* WinUI 3：基于 Fluent Design 的现代化 Windows 前端桌面应用框架。
* Windows 服务：实现开机自启动、后台持久化运行。
* 底层 Windows API / COM 互操作：实现网络状态识别和拨号连接管理功能。
* C#：项目的所有部分使用 C# 编写，框架版本是 .NET 10。

在 UI 侧，WinUI 框架使用了 Windows SDK 自包含来支持非打包应用的运行；在服务侧，使用 Native AOT 来支持无需依赖 .NET 运行时的运行。

平台可用性
---

应用仅可在 Windows 10 （1809 及以后）或更高版本的 Windows 系统中运行。

其他相关信息
---

Reimu's NetSeal 项目中，约 7~10％ 的代码使用 AI 辅助生成。

另请参阅
---

上古老坟 -> https://learn.microsoft.com/windows/win32/api/ras/

WinUI 3 -> https://learn.microsoft.com/windows/apps/winui/winui3/

Windows 中的新式待机 -> https://learn.microsoft.com/windows-hardware/design/device-experiences/networking-power-management-for-modern-standby-platforms