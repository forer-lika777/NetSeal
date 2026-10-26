using NetSeal.Core.Ipc;
using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Services.AppSetting;

public class AppSettings(ISettingsStore settingsStore) : IAppSettings
{
    private readonly ISettingsStore settingsStore = settingsStore;

    /// <summary>
    /// 保存是否处于登录状态的键值。
    /// </summary>
    public IKeyItem<bool> HasSavedNetworkAuth { get; } = new KeyItem<bool>(settingsStore, nameof(HasSavedNetworkAuth), false);

    /// <summary>
    /// 保存已选网络。
    /// </summary>
    public IKeyItem<NetworkItem> SelectedNetwork { get; } = new KeyItem<NetworkItem>(settingsStore, nameof(SelectedNetwork), new NetworkItem(), NetworkItemJsonContext.Default);

    /// <summary>
    /// 断连时是否自动重新连接。
    /// </summary>
    public IKeyItem<bool> ReconnectOnDisconnect { get; } = new KeyItem<bool>(settingsStore, nameof(ReconnectOnDisconnect), false);

    /// <summary>
    /// 从睡眠中唤醒时是否重新连接。
    /// </summary>
    public IKeyItem<bool> ReconnectOnWake { get; } = new KeyItem<bool>(settingsStore, nameof(ReconnectOnWake), false);

    /// <summary>
    /// 开机时，是否自动连接网络。
    /// </summary>
    public IKeyItem<bool> AutoConnectOnStartup { get; } = new KeyItem<bool>(settingsStore, nameof(AutoConnectOnStartup), false);
}