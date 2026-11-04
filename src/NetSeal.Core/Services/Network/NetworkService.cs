using NetSeal.Core.Models;
using NetSeal.Core.Services.Ras;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace NetSeal.Core.Services.Network;

public class NetworkService : INetworkService
{
    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true, CharSet = CharSet.Unicode)]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

    private readonly INetworkAdapterService adapterService;
    private readonly IRasDialer rasDialer;

    public NetworkService(INetworkAdapterService adapterService, IRasDialer rasDialer)
    {
        this.adapterService = adapterService;
        this.rasDialer = rasDialer;
    }

    public bool IsEthernetConnected => adapterService.GetActivePhysicalEthernetAdapters().Any();

    public async Task<NetworkConnectivityLevel> GetConnectivityLevelAsync()
    {
        NLM_CONNECTIVITY connectivity = await Task.Run(() =>
        {
            var clsid = new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B");
            var iid = new Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B");

            // 注意：这里改成 hr < 0
            int hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out IntPtr pUnk);
            if (hr < 0 || pUnk == IntPtr.Zero)
                return NLM_CONNECTIVITY.NLM_CONNECTIVITY_DISCONNECTED;

            // 交给 ComWrappers 接管，千万别手动 Release pUnk
            var wrappers = new StrategyBasedComWrappers();
            var networkListManager = (INetworkListManager)wrappers.GetOrCreateObjectForComInstance(pUnk, CreateObjectFlags.None);

            // 直接调用，不需要 try-finally 去管 pUnk 了
            networkListManager.GetConnectivity(out NLM_CONNECTIVITY conn);
            return conn;
        });

        // === 判断逻辑保持原样 ===
        const NLM_CONNECTIVITY internet = NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_INTERNET
                                        | NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_INTERNET;
        const NLM_CONNECTIVITY local = NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_LOCALNETWORK
                                        | NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_LOCALNETWORK;
        const NLM_CONNECTIVITY subnet = NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_SUBNET
                                        | NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_SUBNET;

        if ((connectivity & internet) != 0)
            return NetworkConnectivityLevel.InternetAccess;
        if ((connectivity & local) != 0)
            return NetworkConnectivityLevel.ConstrainedInternetAccess;
        if ((connectivity & subnet) != 0)
            return NetworkConnectivityLevel.LocalAccess;

        return NetworkConnectivityLevel.None;
    }

    public async Task<List<NetworkAdapter>> GetActiveNetworkAdaptersAsync()
    {
        return await Task.Run(() =>
        {
            var guids = GetActiveNetworkIds();

            return adapterService.GetNetworkAdapters()
                .Where(a => Guid.TryParse(a.Id, out var id) && guids.Contains(id))
                .ToList();
        });
    }

    private unsafe HashSet<Guid> GetActiveNetworkIds()
    {
        return GetAllINetworks()
            .Where(n => n.GetIsConnected())
            .SelectMany(n => GetAllINetworkConnections(n))
            .Select(c => GetConnectionAdapterId(c))
            .ToHashSet();
    }

    public async Task<List<string>> GetActiveNetworkAdapterNamesAsync()
    {
        var adapters = await GetActiveNetworkAdaptersAsync();
        return adapters.Select(a => a.Name).ToList();
    }

    public async Task<List<NetworkItem>> GetActiveNetworksAsync()
    {
        return (await GetAllNetworksAsync()).Where(x => x.IsConnected).ToList();
    }

    public async Task<List<string>> GetActiveNetworkNamesAsync()
    {
        var networks = await GetActiveNetworksAsync();
        return networks.Select(n => n.Name ?? "未知网络名称").ToList();
    }

    /// <summary>
    /// 获取系统存储过的所有逻辑网络。
    /// </summary>
    /// <returns></returns>
    private static List<INetwork> GetAllINetworks()
    {
        var result = new List<INetwork>();

        var clsid = new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B");
        var iid = new Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B");

        
        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1 /*CLSCTX_INPROC_SERVER*/, ref iid, out IntPtr pUnk);
        if (hr != 0 || pUnk == IntPtr.Zero)
            return result;

        var wrappers = new StrategyBasedComWrappers();
        var networkListManager = (INetworkListManager)wrappers.GetOrCreateObjectForComInstance(pUnk, CreateObjectFlags.None);

        // 3. 调用我们改好的 out 方法
        networkListManager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED, out IEnumNetworks networks);

        if (networks == null)
            return result;

        nint buffer = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            uint fetched = 0;
            while (networks.Next(1, buffer, out fetched) == 0 && fetched == 1)
            {
                nint networkPtr = Marshal.ReadIntPtr(buffer);
                if (networkPtr != IntPtr.Zero)
                {
                    var network = (INetwork)wrappers.GetOrCreateObjectForComInstance(networkPtr, CreateObjectFlags.None);
                    result.Add(network);

                    Marshal.Release(networkPtr);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        Marshal.Release(pUnk);

        return result;
    }

    public async Task<List<NetworkItem>> GetAllNetworksAsync()
    {
        var networks = await Task.Run(() => GetAllINetworks());
        var result = new List<NetworkItem>();

        foreach (var network in networks)
        {
            network.GetName(out nint bstr);
            string name = Marshal.PtrToStringBSTR(bstr);
            Marshal.FreeBSTR(bstr);

            result.Add(new NetworkItem
            {
                IsConnected = network.GetIsConnected(),
                Id = GetNetworkId(network),
                Name = name,
            });
        }

        return result;
    }

    private unsafe Guid GetNetworkId(INetwork network)
    {
        Guid id;
        network.GetNetworkId((nint)(&id));
        return id;
    }

    private unsafe Guid GetConnectionAdapterId(INetworkConnection connection)
    {
        return connection.GetAdapterId();
    }

    /// <summary>
    /// 获取输入逻辑网络下的所有网络连接。
    /// </summary>
    /// <param name="network">要操作的逻辑网络</param>
    /// <returns></returns>
    private static List<INetworkConnection> GetAllINetworkConnections(INetwork network)
    {
        network.GetNetworkConnections(out IEnumNetworkConnections connections);

        var result = new List<INetworkConnection>();

        nint[] buffer = new IntPtr[1];

        while (connections.Next(1, buffer, out uint fetched) == 0 && fetched == 1)
        {
            try
            {
                var wrappers = new StrategyBasedComWrappers();

                nint networkConnPtr = buffer[0];

                if (networkConnPtr != IntPtr.Zero)
                {
                    var connection = (INetworkConnection)wrappers.GetOrCreateObjectForComInstance(
                        networkConnPtr,
                        CreateObjectFlags.None);
                    result.Add(connection);
                }

            }
            finally
            {
                Marshal.Release(buffer[0]);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取所有接入了以太网的逻辑网络。
    /// </summary>
    /// <returns></returns>
    private List<NetworkItem> GetAllActiveEthernetAdapterNetworks()
    {
        return GetAllActiveEthernetAdapterNetworks(GetAllINetworks());
    }

    /// <summary>
    /// 获取所有接入了以太网的逻辑网络。
    /// </summary>
    /// <param name="networks">要过滤的逻辑网络</param>
    /// <returns></returns>
    private unsafe List<NetworkItem> GetAllActiveEthernetAdapterNetworks(List<INetwork> networks)
    {
        var physicalIds = new HashSet<Guid>();
        foreach (var ethernet in adapterService.GetActivePhysicalEthernetAdapters())
        {
            if (Guid.TryParse(ethernet.Id, out var id))
            {
                physicalIds.Add(id);
            }
        }

        if (physicalIds.Count == 0)
            return [];

        var result = new List<NetworkItem>();

        foreach (var network in networks)
        {
            var connections = GetAllINetworkConnections(network);
            foreach (var connection in connections)
            {
                var connectionId = GetConnectionAdapterId(connection);
                if (physicalIds.Contains(connectionId))
                {
                    network.GetName(out nint bstr);
                    string name = Marshal.PtrToStringBSTR(bstr);
                    Marshal.FreeBSTR(bstr);

                    result.Add(new NetworkItem
                    {
                        Name = name,
                        Id = GetNetworkId(network),
                        IsConnected = network.GetIsConnected(),
                    });
                    break;
                }
            }
        }

        return result;
    }

    public async Task<List<NetworkItem>> GetAllActiveEthernetAdapterNetworksAsync()
    {
        return await Task.Run(() => GetAllActiveEthernetAdapterNetworks());
    }
}