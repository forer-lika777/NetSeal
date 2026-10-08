using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using NetSeal.Models;
using NetSeal.Services;
using NetSeal.Services.Network;
using NetSeal.Services.Network.Ras;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.PointOfService;
using Windows.Media.DialProtocol;
using Windows.Networking.Connectivity;
using Windows.System;

namespace NetSeal.ViewModels;

public partial class StatusPageModel : ObservableObject
{
    private const string ENTRY_NAME = "NetSeal Connection";

    private readonly IAppSettings appSettings;
    private readonly IUiDispatcher dispatcher;
    private readonly INetworkService networkService;
    private readonly INetworkAdapterService adapterService;
    private readonly IRasDialer rasDialer;

    private DateTime lastHandled = DateTime.MinValue;
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private readonly Lock locker = new();

    private CancellationTokenSource? connectCts = null;

    private bool initialized = false;

    public StatusPageModel(IAppSettings appSettings, IUiDispatcher dispatcher, INetworkService networkService, INetworkAdapterService adapterService, IRasDialer rasDialer)
    {
        this.appSettings = appSettings;
        this.dispatcher = dispatcher;
        this.networkService = networkService;
        this.adapterService = adapterService;
        this.rasDialer = rasDialer;
        _ = InitializeAsync();
    }

    [ObservableProperty]
    public partial ObservableCollection<Network> Networks { get; set; } = [];

    [ObservableProperty]
    public partial bool IsEthernetNetworkAdapterConnected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsOtherNetworkAdapterConnected { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    public partial bool IsPppoeNetworkConnected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsInternetAccess { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool HasAvailableNetworks { get; set; } = false;

    [ObservableProperty]
    public partial bool IsPppoeConnectionManagedByOutside { get; set; } = false;

    [ObservableProperty]
    public partial bool IsNetworkConnectionManagedByOutside { get; set; } = false;

    [ObservableProperty]
    public partial bool IsNotPppoeNetworkConnection { get; set; } = false;

    [ObservableProperty]
    public partial string ActiveNetworkAdapterName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveNetworkName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActivePppoeConnectionEntryName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Network? SelectedNetwork { get; set; } = null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial int SelectedNetworkIndex { get; set; } = -1;

    async partial void OnSelectedNetworkIndexChanged(int oldValue, int newValue)
    {
        if (newValue < 0)
            return;

        if (SelectedNetwork is not null)
        {
            var tcs = new TaskCompletionSource();
            StrongReferenceMessenger.Default.Send<ChangeNetworkConfirmMessage>(new ChangeNetworkConfirmMessage(tcs));

            try
            {
                await tcs.Task.WaitAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                SelectedNetworkIndex = oldValue;
                return;
            }
        }

        SelectedNetwork = Networks[(int)newValue];
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    public partial bool IsConnecting { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial string DisplayMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string AccountId { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string Password { get; set; } = string.Empty;

    private async Task InitializeAsync()
    {
        if (initialized)
            return;

        initialized = true;

        IsLoading = true;

        await UpdateNetworkConnectionsStatusAsync();
        await UpdatePppoeConnectionStatusAsync();

        await dispatcher.InvokeAsync(async () =>
        {
            if (appSettings.HasSavedNetworkAuth.Value)
            {
                AccountId = appSettings.AccountId.Value;
                Password = appSettings.Password.Value;
                SelectedNetwork = appSettings.SelectedNetwork.Value;

                if (!IsNetworkConnectionManagedByOutside)
                {
                    var item = Networks.FirstOrDefault(c => c.Name == SelectedNetwork.Name);

                    if (item is not null)
                        await ConnectAsync();
                }
            }

            if (SelectedNetworkIndex >= 0)
                SelectedNetwork = Networks[(int)SelectedNetworkIndex];

            IsLoading = false;

            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
        });
    }

    private async void NetworkInformation_NetworkStatusChanged(object sender)
    {
        lock (locker)
        {
            var now = DateTime.UtcNow;
            if (now - lastHandled < cooldown)
                return;

            lastHandled = now;
        }

        await UpdateNetworkConnectionsStatusAsync();
        await UpdatePppoeConnectionStatusAsync();
    }

    private async Task UpdateNetworkConnectionsStatusAsync()
    {
        // 获取网络信息是耗时操作，使用异步方法包装以防止阻塞 UI 线程
        List<Network> networks = await Task.Run(() => networkService.GetAllNetworks().Where(x => x.IsConnected).ToList());
        List<Network> ethernetNetworks = await Task.Run(() => networkService.GetAllEthernetAdapterNetworks());

        ConnectionProfile? profile = await Task.Run(() => NetworkInformation.GetInternetConnectionProfile());
        NetworkConnectivityLevel? level = profile?.GetNetworkConnectivityLevel() ?? NetworkConnectivityLevel.None;

        // 回到 UI 线程
        await dispatcher.InvokeAsync(() =>
        {
            for (int i = Networks.Count - 1; i >= 0; i--)
            {
                if (!ethernetNetworks.Any(c => c.Name == Networks[i].Name))
                    Networks.RemoveAt(i);
            }

            foreach (var network in ethernetNetworks)
            {
                if (!Networks.Any(c => c.Name == network.Name))
                    Networks.Add(network);
            }

            foreach (var network in Networks)
            {
                if (network.Name == SelectedNetwork?.Name)
                    network.IsSelected = true;
            }

            HasAvailableNetworks = Networks.Count > 0;

            // 检查是否连接到互联网
            IsInternetAccess = level == NetworkConnectivityLevel.InternetAccess;

            // 检查系统网络接入状态：可访问互联网、可访问受限的互联网（需要提供网络认证）、可访问本地局域网
            if (level != NetworkConnectivityLevel.None)
            {
                ActiveNetworkAdapterName = profile?.ProfileName ?? "无网络连接";
                if (networks.Count != 0)
                {
                    ActiveNetworkName = string.Empty;

                    foreach (var network in networks)
                    {
                        if (string.IsNullOrWhiteSpace(ActiveNetworkName))
                        {
                            ActiveNetworkName = network.Name ?? "未知网络名称";
                        }
                        else
                        {
                            ActiveNetworkName += $", {network.Name ?? "未知网络名称"}";
                        }
                    }
                }
                else
                {
                    ActiveNetworkName = "未知网络连接";
                }

                bool isEthernet = false;

                // 非 Wi-Fi 网络和移动蜂窝网络表明该配置是有线网络适配器（但可能是虚拟网络适配器）
                if (profile is not null && !profile.IsWlanConnectionProfile && !profile.IsWwanConnectionProfile) 
                {
                    List<NetworkInterface> interfaces = adapterService.GetPhysicalEthernetAdapters();

                    // 与物理网络适配器（以太网）比对 Id
                    foreach (var ni in interfaces)
                    {
                        if (Guid.TryParse(ni.Id, out var niId) && niId == profile.NetworkAdapter.NetworkAdapterId)
                        {
                            // 此网络适配器明确为以太网。
                            isEthernet = true;
                            break;
                        }
                    }

                    // 但如果此网络适配器是拨号连接，则不会匹配以太网适配器 id，而是使用自己的网络适配器 id。还需要后续判断
                }

                if (isEthernet)
                {
                    // 有接入网络的网络适配器，且该适配器是以太网
                    IsEthernetNetworkAdapterConnected = true;
                    IsOtherNetworkAdapterConnected = false;
                }
                else
                {
                    // 有接入网络的网络适配器，但该适配器不是以太网（可能是无线网络或虚拟网卡）
                    IsEthernetNetworkAdapterConnected = false;
                    IsOtherNetworkAdapterConnected = true;
                }
            }
            else
            {
                // 没有接入网络的网络适配器
                IsEthernetNetworkAdapterConnected = IsOtherNetworkAdapterConnected = false;
            }

            if (!appSettings.HasSavedNetworkAuth.Value)
            {
                ShowDisplayMessage("在网络接口列表中选择要连接的网络。");
                return;
            }

            if (!HasAvailableNetworks)
            {
                ShowDisplayMessage("未找到可用的网络连接。");
                return;
            }
        });
    }

    private async Task UpdatePppoeConnectionStatusAsync()
    {
        var connection = await Task.Run(() => rasDialer.GetActivePppoeConnection());

        await dispatcher.InvokeAsync(() =>
        {
            // 当前系统接入了以太网连接（可能不是 pppoe 连接）
            if (connection is NativeMethods.RASCONN conn)
            {
                // 有 pppoe 连接。
                IsPppoeNetworkConnected = true;
                ActivePppoeConnectionEntryName = conn.entryName;
                // 此 pppoe 连接是当前应用程序管理的 pppoe 连接
                IsPppoeConnectionManagedByOutside = ActivePppoeConnectionEntryName != ENTRY_NAME;
                // pppoe 连接一定使用以太网适配器
                IsEthernetNetworkAdapterConnected = true;
                IsOtherNetworkAdapterConnected= false;
            }
            else
            {
                // 无 pppoe 连接
                IsPppoeNetworkConnected = false;
                ActivePppoeConnectionEntryName = string.Empty;
                // 此 pppoe 连接不是当前应用程序管理的 pppoe 连接
                IsPppoeConnectionManagedByOutside = false;
            }

            // 有 pppoe 连接，且此连接是当前应用程序管理的 pppoe 连接，表明系统的网络连接由当前应用程序管理
            IsNetworkConnectionManagedByOutside = !(IsPppoeNetworkConnected && !IsPppoeConnectionManagedByOutside);

            // 当前网络已接入互联网，但是没有 pppoe 连接，表明网络是非 pppoe 类型（例如直接接入的家庭宽带网络）
            IsNotPppoeNetworkConnection = IsInternetAccess && !IsPppoeNetworkConnected;
        });
    }

    private bool CanConnect()
    {
        if (IsLoading)
            return false;

        // 以太网未连接
        if (!IsEthernetNetworkAdapterConnected)
            return false;

        // pppoe 宽带连接已连接
        if (IsPppoeNetworkConnected)
            return false;

        // 有可用网络
        if (!HasAvailableNetworks)
            return false;

        // 用户名或密码为空
        if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(Password))
        {
            ShowDisplayMessage("请输入用户名或密码");
            return false;
        }

        // 没有匹配的逻辑网络
        if (!Networks.Any(it => it.Name == SelectedNetwork?.Name))
        {
            ShowDisplayMessage("在网络列表中没有找到目标网络连接项。");
            return false;
        }

        return true;
    }

    private bool CanDisconnect()
    {
        if (IsLoading)
            return false;

        // 以太网未连接
        if (!IsEthernetNetworkAdapterConnected)
            return false;

        // pppoe 网络未连接
        if (!IsPppoeNetworkConnected)
            return false;

        // 外部控制的 pppoe 网络（仅依据条目名称判断）
        if (IsPppoeConnectionManagedByOutside)
            return false;

        if (IsConnecting)
            return true;

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsConnecting = true;

        if (!rasDialer.EntryExists(null, ENTRY_NAME))
        {
            rasDialer.CreateEntry(null, ENTRY_NAME, AccountId, Password);
        }

        await ExcuteConnectTaskAsync();

        IsConnecting = false;
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        if (IsConnecting)
        {
            CancelCts(ref connectCts);
            return;
        }

        await ExcuteDisconnectTaskAsync();

        IsConnecting = false;
    }

    private async Task ExcuteConnectTaskAsync()
    {
        ResetCts(ref connectCts);

        try
        {
            await Task.Run(() => rasDialer.Connect(null, ENTRY_NAME, AccountId, Password), connectCts!.Token);

            appSettings.HasSavedNetworkAuth.Value = true;
            appSettings.AccountId.Value = AccountId;
            appSettings.Password.Value = Password;

            if (SelectedNetwork is not null)
                appSettings.SelectedNetwork.Value = SelectedNetwork;
        }
        catch (Win32Exception ex)
        {
            await dispatcher.InvokeAsync(() => ShowDisplayMessage(ex));
        }
    }

    private async Task ExcuteDisconnectTaskAsync()
    {
        var task = new Task(async () => {
            try
            {
                rasDialer.Disconnect(ENTRY_NAME);
            }
            catch (Win32Exception ex)
            {
                await dispatcher.InvokeAsync(() => ShowDisplayMessage(ex));
            }
        });

        task.Start();
        await task.WaitAsync(CancellationToken.None);
    }

    private void ShowDisplayMessage(Exception ex)
    {
        Debug.WriteLine(ex);
        DisplayMessage = ex.Message;
    }

    private void ShowDisplayMessage(string message)
    {
        Debug.WriteLine(message);
        DisplayMessage = message;
    }

    private static void ResetCts(ref CancellationTokenSource? cts, CancellationTokenSource? ctsToUse = null)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = ctsToUse ?? new CancellationTokenSource();
    }

    private static void CancelCts(ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
