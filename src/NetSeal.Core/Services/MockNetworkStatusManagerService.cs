using CommunityToolkit.Mvvm.Messaging;
using NetSeal.Core.Models;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;

namespace NetSeal.Core.Services;

public class MockNetworkStatusManagerService : INetworkService, INetworkAdapterService, IRasDialer
{
    //private bool ethernetConnected = true;
    private bool pppoeConnected = false;
    private RasConnection? rasConnection = null;
    private List<NetworkAdapter> adapters;

    public MockNetworkStatusManagerService()
    {
        adapters = [new NetworkAdapter { Name = "以太网", Description = "你妈的以太网", Id = "666" }];
    }

    public bool IsEthernetConnected { get; private set; } = true;

    public NetworkConnectivityLevel ConnectivityLevel { get; private set; } = NetworkConnectivityLevel.ConstrainedInternetAccess;

    public async Task<List<string>> GetActiveNetworkAdapterNamesAsync()
    {
        return ["以太网"];
    }

    public async Task<List<NetworkAdapter>> GetActiveNetworkAdaptersAsync()
    {
        return adapters;
    }

    public async Task<List<string>> GetActiveNetworkNamesAsync()
    {
        return ["网络"];
    }

    public async Task<List<NetworkItem>> GetActiveNetworksAsync()
    {
        return [new NetworkItem { Name = "网络", IsConnected = true }];
    }

    public List<NetworkAdapter> GetActivePhysicalEthernetAdapters()
    {
        return adapters;
    }

    public async Task<RasConnection?> GetActiveRasConnectionAsync()
    {
        await Task.Delay(1000);
        return rasConnection;
    }

    public async Task<List<NetworkItem>> GetAllActiveEthernetAdapterNetworksAsync()
    {
        await Task.Delay(1000);
        List<NetworkItem> results = [new NetworkItem { Name = "网络", IsConnected = true }];
        return results;
    }

    public async Task<List<NetworkItem>> GetAllNetworksAsync()
    {
        await Task.Delay(1000);
        List<NetworkItem> results = [new NetworkItem { Name = "网络", IsConnected = true }];
        return results;
    }

    public async Task<NetworkConnectivityLevel> GetConnectivityLevelAsync()
    {
        
        return ConnectivityLevel;
    }

    public async Task<bool> GetIsConnectionManagedByCurrentSessionAsync()
    {
        throw new NotImplementedException();
    }

    public List<NetworkAdapter> GetNetworkAdapters()
    {
        return adapters;
    }

    public List<NetworkAdapter> GetPhysicalEthernetAdapters()
    {
        return adapters;
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
        StrongReferenceMessenger.Default.Send<NetworkInformationChangedMessage>(new NetworkInformationChangedMessage());
    }

    public async Task RasCreateEntryAsync(string? phoneBook, string entryName, string? username, string? password)
    {
        await Task.Delay(500);
    }

    public async Task RasDisconnectAsync(string? phoneBook, string entryName, CancellationToken cancellationToken = default)
    {
        await Task.Delay(2000, cancellationToken);
        rasConnection = null;
        pppoeConnected = false;
        ConnectivityLevel = NetworkConnectivityLevel.ConstrainedInternetAccess;
        StrongReferenceMessenger.Default.Send<NetworkInformationChangedMessage>(new NetworkInformationChangedMessage());
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
