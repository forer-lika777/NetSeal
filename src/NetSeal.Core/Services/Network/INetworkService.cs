using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSeal.Core.Services.Network;

public interface INetworkService
{
    public bool IsEthernetConnected { get; }
    public Task<NetworkConnectivityLevel> GetConnectivityLevelAsync();
    public Task<List<Models.Network>> GetActiveNetworksAsync();
    public Task<List<string>> GetActiveNetworkNamesAsync();
    public Task<List<NetworkAdapter>> GetActiveNetworkAdaptersAsync();
    public Task<List<string>> GetActiveNetworkAdapterNamesAsync();
    public Task<List<Models.Network>> GetAllNetworksAsync();
    public Task<List<Models.Network>> GetAllEthernetAdapterNetworksAsync();
}
