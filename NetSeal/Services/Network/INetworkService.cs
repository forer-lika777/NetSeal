using NetSeal.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Services.Network;

public interface INetworkService
{
    public List<Models.Network> GetAllNetworks();
    public List<Models.Network> GetAllEthernetAdapterNetworks();
}
