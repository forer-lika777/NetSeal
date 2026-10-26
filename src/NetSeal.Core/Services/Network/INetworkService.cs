using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSeal.Core.Models;

namespace NetSeal.Core.Services.Network;

public interface INetworkService
{
    public bool IsEthernetConnected { get; }
    public Task<NetworkConnectivityLevel> GetConnectivityLevelAsync();
    public Task<List<NetworkItem>> GetActiveNetworksAsync();
    public Task<List<string>> GetActiveNetworkNamesAsync();
    public Task<List<NetworkAdapter>> GetActiveNetworkAdaptersAsync();
    public Task<List<string>> GetActiveNetworkAdapterNamesAsync();
    public Task<List<NetworkItem>> GetAllNetworksAsync();
    public Task<List<NetworkItem>> GetAllEthernetAdapterNetworksAsync();
}
