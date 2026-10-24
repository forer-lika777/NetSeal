using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace NetSeal.Core.Services.Network;

public interface INetworkAdapterService
{
    public List<NetworkAdapter> GetNetworkAdapters();
    public List<NetworkAdapter> GetPhysicalEthernetAdapters();
    public List<NetworkAdapter> GetActivePhysicalEthernetAdapters();
}
