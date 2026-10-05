using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using NetSeal.Models;
using NetSeal.Services;
using NetSeal.Services.Network;
using NetSeal.Services.Ras;
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
    private readonly IRasDialer rasDialer;

    private DateTime lastHandled = DateTime.MinValue;
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private readonly Lock locker = new();

    private CancellationTokenSource? connectCts = null;

    private bool initialized = false;

    public StatusPageModel(IAppSettings appSettings, IUiDispatcher dispatcher, IRasDialer rasDialer)
    {
        this.appSettings = appSettings;
        this.dispatcher = dispatcher;
        this.rasDialer = rasDialer;
        _ = InitializeAsync();
    }

    [ObservableProperty]
    public partial ObservableCollection<NetworkNameDisplayStatus> NetworkConnections { get; set; } = [];

    [ObservableProperty]
    public partial bool IsEthernetNetworkInterfaceConnected { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisConnectCommand))]
    public partial bool IsPppoeNetworkConnected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsOtherNetworkInterfaceConnected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsInternetAccess { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool HasAvailableConnections { get; set; } = false;

    [ObservableProperty]
    public partial bool IsPppoeConnectionManagedByOutside { get; set; } = false;

    [ObservableProperty]
    public partial bool IsNetworkConnectionManagedByOutside { get; set; } = false;

    [ObservableProperty]
    public partial string ActivePppoeConnectionEntryName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveNetworkConnectionName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedConnectionName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial int SelectedConnectionIndex { get; set; } = -1;

    async partial void OnSelectedConnectionIndexChanged(int oldValue, int newValue)
    {
        if (newValue < 0)
            return;

        if (!string.IsNullOrEmpty(SelectedConnectionName))
        {
            var tcs = new TaskCompletionSource();
            StrongReferenceMessenger.Default.Send<ChangeNetworkConfirmMessage>(new ChangeNetworkConfirmMessage(tcs));

            try
            {
                await tcs.Task.WaitAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                SelectedConnectionIndex = oldValue;
                return;
            }
        }

        SelectedConnectionName = NetworkConnections[(int)newValue].Name;
    }

    //[ObservableProperty]
    //[NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    //[NotifyCanExecuteChangedFor(nameof(DisConnectCommand))]
    //public partial bool IsConnected { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisConnectCommand))]
    public partial bool IsConnecting { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisConnectCommand))]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial string DisplayMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string AccountId { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string Password { get; set; } = string.Empty;

    public static IEnumerable<NetworkConnection> GetAllNics()
    {
        return NetworkConnectionService.GetAllPhysicalAdapterNetworks();
    }

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
            if (appSettings.HasSavedConnectionAuth.Value)
            {
                AccountId = appSettings.AccountId.Value;
                Password = appSettings.Password.Value;
                SelectedConnectionName = appSettings.SelectedConnectionName.Value;

                if (!IsNetworkConnectionManagedByOutside)
                {
                    var item = NetworkConnections.FirstOrDefault(c => c.Name == appSettings.SelectedConnectionName.Value);

                    if (item is not null)
                        await ConnectAsync();
                }
            }

            if (SelectedConnectionIndex >= 0)
                SelectedConnectionName = NetworkConnections[(int)SelectedConnectionIndex].Name;

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
        var connections = await Task.Run(() => GetAllNics().ToList());
        var profile = await Task.Run(() => NetworkInformation.GetInternetConnectionProfile());
        var level = profile.GetNetworkConnectivityLevel();

        await dispatcher.InvokeAsync(() =>
        {
            for (int i = NetworkConnections.Count - 1; i >= 0; i--)
            {
                if (!connections.Any(c => c.Name == NetworkConnections[i].Name))
                    NetworkConnections.RemoveAt(i);
            }

            foreach (var connection in connections)
            {
                if (!NetworkConnections.Any(c => c.Name == connection.Name))
                    NetworkConnections.Add(new NetworkNameDisplayStatus(connection.Name));
            }

            foreach (var connection in NetworkConnections)
            {
                if (connection.Name == SelectedConnectionName)
                    connection.Selected = true;
            }

            HasAvailableConnections = NetworkConnections.Count > 0;

            // 检查是否连接到互联网。
            IsInternetAccess = level == NetworkConnectivityLevel.InternetAccess;

            // 检查系统网络接入状态：可访问互联网、可访问受限的互联网（需要提供网络认证）、可访问本地局域网。
            if (level != NetworkConnectivityLevel.None)
            {
                ActiveNetworkConnectionName = profile.ProfileName;
                bool isEthernet = false;

                // 非 Wi-Fi 网络和移动蜂窝网络表明该配置是有线网络适配器（但可能是虚拟网络适配器）。
                if (!profile.IsWlanConnectionProfile && !profile.IsWwanConnectionProfile) 
                {
                    //var id = profile.NetworkAdapter.NetworkAdapterId;
                    //string target = id.ToString("D");

                    // 与物理网络适配器（以太网）比对 Id。
                    foreach (var ni in NetworkAdapterService.GetPhysicalEthernetAdapters())
                    {
                        if (Guid.TryParse(ni.Id, out var niId) && niId == profile.NetworkAdapter.NetworkAdapterId)
                        {
                            // 此网络适配器明确为以太网。
                            isEthernet = true;
                            break;
                        }
                    }
                }

                if (isEthernet)
                {
                    // 有接入网络的网络适配器，且该适配器是以太网。
                    IsEthernetNetworkInterfaceConnected = true;
                    IsOtherNetworkInterfaceConnected = false;
                }
                else
                {
                    // 有接入网络的网络适配器，但该适配器不是以太网（可能是无线网络或虚拟网卡）。
                    IsEthernetNetworkInterfaceConnected = false;
                    IsOtherNetworkInterfaceConnected = true;
                }
            }
            else
            {
                // 没有接入网络的网络适配器。
                IsEthernetNetworkInterfaceConnected = IsOtherNetworkInterfaceConnected = false;
            }

            if (!appSettings.HasSavedConnectionAuth.Value)
            {
                ShowDisplayMessage("在网络接口列表中选择要连接的网络。");
                return;
            }

            if (!HasAvailableConnections)
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
            if (IsEthernetNetworkInterfaceConnected)
            {
                // 当前系统接入了以太网连接（可能不是 pppoe 连接）。
                if (connection is NativeMethods.RASCONN conn)
                {
                    // 有 pppoe 连接。
                    IsPppoeNetworkConnected = true;
                    ActivePppoeConnectionEntryName = conn.entryName;
                    // 此 pppoe 连接是当前应用程序管理的 pppoe 连接。
                    IsPppoeConnectionManagedByOutside = ActivePppoeConnectionEntryName != ENTRY_NAME;
                }
                else
                {
                    // 无 pppoe 连接
                    IsPppoeNetworkConnected = false;
                    ActivePppoeConnectionEntryName = string.Empty;
                    // 此 pppoe 连接不是当前应用程序管理的 pppoe 连接。
                    IsPppoeConnectionManagedByOutside = false;
                }
            }
            else
            {
                // 当前系统没有接入以太网连接（可能是无线网络连接或无网络连接），无 pppoe 连接（pppoe 网络连接要求接入以太网）。
                IsPppoeNetworkConnected = false;
            }

            // 有 pppoe 连接，且此连接是当前应用程序管理的 pppoe 连接，表明系统的网络连接由当前应用程序管理。
            IsNetworkConnectionManagedByOutside = !(IsPppoeNetworkConnected && !IsPppoeConnectionManagedByOutside);
        });
    }

    private bool CanConnect()
    {
        if (IsLoading)
            return false;

        // 以太网未连接。
        if (!IsEthernetNetworkInterfaceConnected)
            return false;

        // pppoe 宽带连接已连接。
        if (IsPppoeNetworkConnected)
            return false;

        // 有可用网络。
        if (!HasAvailableConnections)
            return false;

        // 用户名或密码为空。
        if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(Password))
        {
            ShowDisplayMessage("请输入用户名或密码");
            return false;
        }

        // 没有匹配的逻辑网络。
        if (!NetworkConnections.Any(it => it.Name == SelectedConnectionName))
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

        // 以太网未连接。
        if (!IsEthernetNetworkInterfaceConnected)
            return false;

        // pppoe 网络未连接。
        if (!IsPppoeNetworkConnected)
            return false;

        // 外部控制的 pppoe 网络（仅依据条目名称判断）。
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
    private async Task DisConnectAsync()
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

        var task = new Task(async () => {
            try
            {
                rasDialer.Connect(null, ENTRY_NAME, AccountId, Password);

                appSettings.HasSavedConnectionAuth.Value = true;
                appSettings.AccountId.Value = AccountId;
                appSettings.Password.Value = Password;
                appSettings.SelectedConnectionName.Value = SelectedConnectionName;
            }
            catch (Win32Exception ex)
            {
                await dispatcher.InvokeAsync(() => ShowDisplayMessage(ex));
            }
        });

        task.Start();

        try
        {
            await task.WaitAsync(connectCts!.Token);
        }
        catch (OperationCanceledException)
        {
            return;
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
