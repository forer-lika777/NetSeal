using NetSeal.Core.Models;

namespace NetSeal.Core.Services.Network;

public interface INetworkAdapterService
{
    public List<NetworkAdapter> GetNetworkAdapters();
    public List<NetworkAdapter> GetPhysicalEthernetAdapters();
    public List<NetworkAdapter> GetActivePhysicalEthernetAdapters();
}
