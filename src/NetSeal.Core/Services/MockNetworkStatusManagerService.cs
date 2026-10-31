using CommunityToolkit.Mvvm.Messaging;
using NetSeal.Core.Models;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;

namespace NetSeal.Core.Services;

public class MockNetworkStatusManagerService : ICoreManager, IRasDialer
{
    //private bool ethernetConnected = true;
    private bool pppoeConnected = false;
    private RasConnection? rasConnection = null;
    private List<NetworkAdapter> adapters;

    public MockNetworkStatusManagerService()
    {
        adapters = [new NetworkAdapter { Name = "以太网", Description = "你妈的以太网", Id = "666" }];
    }

    public event EventHandler? NetworkStatusChanged;

    private NetworkConnectivityLevel ConnectivityLevel = NetworkConnectivityLevel.ConstrainedInternetAccess;

    private async Task<List<string>> GetActiveNetworkAdapterNamesAsync()
    {
        return ["以太网"];
    }

    private async Task<List<NetworkAdapter>> GetActiveNetworkAdaptersAsync()
    {
        return adapters;
    }

    private async Task<List<string>> GetActiveNetworkNamesAsync()
    {
        return ["网络"];
    }

    private async Task<List<NetworkItem>> GetActiveNetworksAsync()
    {
        return [new NetworkItem { Name = "网络", IsConnected = true }];
    }

    private List<NetworkAdapter> GetActivePhysicalEthernetAdapters()
    {
        return adapters;
    }

    private async Task<List<NetworkItem>> GetAllActiveEthernetAdapterNetworksAsync()
    {
        await Task.Delay(1000);
        List<NetworkItem> results = [new NetworkItem { Name = "网络", IsConnected = true }];
        return results;
    }

    private async Task<List<NetworkItem>> GetAllNetworksAsync()
    {
        await Task.Delay(1000);
        List<NetworkItem> results = [new NetworkItem { Name = "网络", IsConnected = true }];
        return results;
    }

    private async Task<NetworkConnectivityLevel> GetConnectivityLevelAsync()
    {
        
        return ConnectivityLevel;
    }

    private List<NetworkAdapter> GetNetworkAdapters()
    {
        return adapters;
    }

    private List<NetworkAdapter> GetPhysicalEthernetAdapters()
    {
        return adapters;
    }

    public async Task<NetworkStatus> GetNetworkStatusAsync()
    {
        await Task.Delay(500);
        var status = new NetworkStatus
        {
            ActiveEthernetNetworks = await GetActiveNetworksAsync(),
            ActiveNetworkAdapterNames = string.Join(",", await GetActiveNetworkAdapterNamesAsync()),
            ActiveNetworkNames = string.Join(",", await GetActiveNetworkNamesAsync()),
        };

        status.IsEthernetConnected = true;

        status.IsPppoeNetworkConnected = pppoeConnected;
        status.NetworkConnectivityLevel = ConnectivityLevel;
        status.IsInternetAccess = pppoeConnected;

        status.ActivePppoeConnectionEntryName = pppoeConnected ? RasEntryName.NetSealServiceEntryName : "";

        status.IsNotPppoeNetworkConnection = false;

        status.IsNetworkConnectionManagedByOutside = false;
        status.IsPppoeConnectionManagedByOutside = true;

        return status;
    }

    public async Task<RasConnection?> GetActiveRasConnectionAsync()
    {
        await Task.Delay(1000);
        return rasConnection;
    }

    public async Task<bool> GetIsConnectionManagedByCurrentSessionAsync()
    {
        return true;
    }

    public async Task<bool> IsConnectionManagedByCurrentSession(RasConnection connection)
    {
        return true;
    }

    public async Task RasConnectAsync(string? phoneBook, string entryName, string username, string password, CancellationToken cancellationToken = default)
    {
        await Task.Delay(2000, cancellationToken);
        rasConnection = new RasConnection { EntryName = entryName, Handle = IntPtr.Zero };
        pppoeConnected = true;
        ConnectivityLevel = NetworkConnectivityLevel.InternetAccess;
        NetworkStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task RasCreateEntryAsync(string? phoneBook, string entryName, string? username, string? password)
    {
        await Task.Delay(500);
    }

    public async Task RasDisconnectAsync(string? entryName, CancellationToken cancellationToken = default)
    {
        await Task.Delay(2000, cancellationToken);
        rasConnection = null;
        pppoeConnected = false;
        ConnectivityLevel = NetworkConnectivityLevel.ConstrainedInternetAccess;
        NetworkStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task RasRemoveEntryAsync(string? phoneBook, string entryName)
    {
        throw new NotImplementedException();
    }

    public Task RasSetCredentialsAsync(string? phoneBook, string entryName, string username, string password)
    {
        throw new NotImplementedException();
    }
}
