Reimu's NetSeal
===

Reimu's NetSeal 是 forer4433 在广东工业大学自动网络拨号连接的 Windows 桌面应用。

Reimu's NetSeal 的功能
---

* 开机登录前连接功能：基于 windows 服务的体系，帮助你在开机时（账户登录前）即可进行自动网络拨号连接。
* 专一网络识别功能：识别并链接到特定网络，仅连接到特定网络时才进行拨号连接。
* 高级连接管理功能：开机自启动连接、睡眠中唤醒连接、连接意外断开时重新连接。

Reimu's NetSeal 的技术栈
---
* 底层 windows api / com 互操作：实现网络识别、拨号连接功能。
* windows 服务：开机自启动连接、连接持久化功能。
* WinUI 3：基于 Fluent UI 的现代化 windows 桌面应用框架。
* C#：项目的所有部分使用 C# 编写。

平台可用性
---

应用只能在 Windows 10 （1809以后）或更高版本的 Windows 系统中运行，因其大部分技术栈仅在 Windows 中可用。

不会考虑跨平台设计。