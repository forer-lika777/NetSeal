using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace NetSeal.Services.Network;

public interface INetworkAdapterService
{
    public List<NetworkInterface> GetPhysicalEthernetAdapters();
}
