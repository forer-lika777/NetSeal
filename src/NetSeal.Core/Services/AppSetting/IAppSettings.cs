using NetSeal.Core.Models;

namespace NetSeal.Core.Services.AppSetting;

public interface IAppSettings
{
    public IKeyItem<bool> HasSavedNetworkAuth { get; }
    public IKeyItem<NetworkItem> SelectedNetwork { get; }
    public IKeyItem<AutoConnectPolicy> AutoConnectPolicy { get; }
    public IKeyItem<RasCredentials> RasCredentials { get; }
}
