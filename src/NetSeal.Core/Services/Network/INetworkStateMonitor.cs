using NetSeal.Core.Models;

namespace NetSeal.Core.Services.Network;

public interface INetworkStateMonitor
{
    public Task<NetworkState> GetNetworkStateAsync();
    public Task ForceRefreshAsync();
    public event EventHandler? NetworkStateChanged;
    public void Register();
    public void Unregister();
}
