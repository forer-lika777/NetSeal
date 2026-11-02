using Microsoft.Extensions.DependencyInjection;
using NetSeal.Core.Models;

namespace NetSeal.Core.Services.AppSetting;

public class NetworkSettings([FromKeyedServices("service")] ISettingsStore settingsStore) : INetworkSettings
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
    /// 自动连接配置。
    /// </summary>
    public IKeyItem<AutoConnectPolicy> AutoConnectPolicy { get; } = new KeyItem<AutoConnectPolicy>(settingsStore, nameof(AutoConnectPolicy), new AutoConnectPolicy(), AutoConnectPolicyJsonContext.Default);

    /// <summary>
    /// 登录凭据。
    /// </summary>
    public IKeyItem<RasCredentials> RasCredentials { get; } = new SecureKeyItem<RasCredentials>(settingsStore, nameof(RasCredentials), new RasCredentials(), RasCredentialsJsonContext.Default);
}