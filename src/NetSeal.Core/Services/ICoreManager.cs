using NetSeal.Core.Models;

namespace NetSeal.Core.Services;

public interface ICoreManager
{
    public Task<NetworkStatus> GetNetworkStatusAsync();
    public event EventHandler? NetworkStatusChanged;
}
