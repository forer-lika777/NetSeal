using NetSeal.Core.Models;

namespace NetSeal.Core.Services;

public interface ICoreNetworkManager
{
    public Task<NetworkStatus> GetNetworkStatusAsync();
    public Task ForceRefreshAsync();
    public event EventHandler? NetworkStatusChanged;
}
