using NetSeal.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Services.Network;

public interface INetworkConnectionService
{
    public List<INetwork> GetAllNetworks();
    public List<INetworkConnection> GetAllNetworkConnections(INetwork network);
    public List<NetworkConnection> GetAllPhysicalAdapterNetworks();
}
