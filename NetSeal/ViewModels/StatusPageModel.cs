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

    private DateTime lastHandled = DateTime.MinValue;
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private readonly Lock locker = new();

    private CancellationTokenSource? connectCts = null;

    private bool initialized = false;

    public StatusPageModel(IAppSettings appSettings, IUiDispatcher dispatcher)
    {
        this.appSettings = appSettings;
        this.dispatcher = dispatcher;
        _ = InitializeAsync();
    }

    [ObservableProperty]
    public partial ObservableCollection<NetworkNameDisplayStatus> NetworkConnections { get; set; } = [];

    [ObservableProperty]
    public partial bool IsEthernetNetworkInterfaceConnected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsOtherNetworkInterfaceConnected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsInternetAccess { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool HasAvailableConnections { get; set; } = false;

    [ObservableProperty]
    public partial bool PppoeConnectionManagedByOutside { get; set; } = false;

    [ObservableProperty]
    public partial string ActivePppoeConnectionEntryName { get; set; } = string.Empty;

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisConnectCommand))]
    public partial bool IsConnected { get; set; } = false;

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
        return NetworkConnectionService.GetAllConnections();
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

                if (!PppoeConnectionManagedByOutside)
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
        var profile = NetworkInformation.GetInternetConnectionProfile();
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
            
            if (level == NetworkConnectivityLevel.InternetAccess)
            {
                IsInternetAccess = true;
            }

            if (level == NetworkConnectivityLevel.LocalAccess || level == NetworkConnectivityLevel.ConstrainedInternetAccess)
            {
                
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
        NativeMethods.RASCONN? connection;

        try
        {
            connection = await Task.Run(() => RasDialer.GetActivePppoeConnection());
        }
        catch (Win32Exception ex)
        {
            await dispatcher.InvokeAsync(() => ShowDisplayMessage(ex.Message));
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            ActivePppoeConnectionEntryName = connection?.entryName ?? string.Empty;

            if (connection is not null)
            {
                if (connection?.entryName != ENTRY_NAME)
                    PppoeConnectionManagedByOutside = true;
                IsConnected = true;
            }
            else
            {
                PppoeConnectionManagedByOutside = false;
                IsConnected = false;
            }
        });
    }

    private bool CanConnect()
    {
        if (IsLoading)
            return false;

        if (IsConnected)
            return false;

        if (!HasAvailableConnections)
            return false;

        if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(Password))
            return false;

        if (PppoeConnectionManagedByOutside)
            return false;

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

        if (IsConnecting)
            return true;

        if (!IsConnected)
            return false;

        if (PppoeConnectionManagedByOutside)
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (IsConnected || IsConnecting)
            return;

        if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(Password))
        {
            ShowDisplayMessage("请输入用户名或密码");
            return;
        }

        IsConnecting = true;

        if (!RasDialer.EntryExists(ENTRY_NAME))
        {
            RasDialer.CreateEntry(ENTRY_NAME, AccountId, Password);
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

        IsConnected = false;
        IsConnecting = false;
    }

    private async Task ExcuteConnectTaskAsync()
    {
        ResetCts(ref connectCts);

        var task = new Task(async () => {
            try
            {
                RasDialer.Connect(ENTRY_NAME, AccountId, Password);

                await dispatcher.InvokeAsync(() => IsConnected = true);

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
                RasDialer.Disconnect(ENTRY_NAME);

                await dispatcher.InvokeAsync(() => IsConnected = false);
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
