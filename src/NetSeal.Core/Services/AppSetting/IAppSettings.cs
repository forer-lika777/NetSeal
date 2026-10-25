using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NetSeal.Core.Models;

namespace NetSeal.Core.Services.AppSetting;

public interface IAppSettings
{
    public IKeyItem<bool> HasSavedNetworkAuth { get; }
    public IKeyItem<Models.Network> SelectedNetwork { get; }
    public IKeyItem<bool> ReconnectOnDisconnect { get; }
    public IKeyItem<bool> ReconnectOnWake { get; }
    public IKeyItem<bool> AutoConnectOnStartup { get; }
}
