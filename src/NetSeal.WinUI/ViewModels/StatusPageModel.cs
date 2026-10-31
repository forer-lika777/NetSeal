using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetSeal.Core.Models;
using NetSeal.Core.Services;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Pipe;
using NetSeal.Core.Services.Ras;
using NetSeal.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.PointOfService;
using Windows.Media.DialProtocol;
using Windows.System;

namespace NetSeal.WinUI.ViewModels;

public partial class StatusPageModel : ObservableObject
{
    private readonly IAppSettings settings;
    private readonly IUiDispatcher dispatcher;
    private readonly ICoreManager coreManager;
    private readonly PipeClient pipeClient;

    private DateTime lastHandled = DateTime.MinValue;
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private readonly Lock locker = new();

    private CancellationTokenSource? connectCts = null;
    private bool initialized = false;

    public StatusPageModel(
        IAppSettings appSettings, 
        IUiDispatcher dispatcher, 
        ICoreManager coreManager, 
        PipeClient pipeClient)
    {
        this.settings = appSettings;
        this.dispatcher = dispatcher;
        this.coreManager = coreManager;
        this.pipeClient = pipeClient;
        _ = InitializeAsync();
        coreManager.NetworkStatusChanged += CoreManager_NetworkStatusChanged;
    }

    private async void CoreManager_NetworkStatusChanged(object? sender, EventArgs e)
    {
        lock (locker)
        {
            var now = DateTime.UtcNow;
            if (now - lastHandled < cooldown)
                return;

            lastHandled = now;
        }

        await UpdateNetworkStatusAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifySelectedNetworkIndex))]
    [NotifyPropertyChangedFor(nameof(CanModifyCredentials))]
    [NotifyPropertyChangedFor(nameof(CanModifyAutoConnectOnStartup))]
    [NotifyPropertyChangedFor(nameof(CanModifyReconnectOnWake))]
    [NotifyPropertyChangedFor(nameof(CanModifyReconnectOnDisconnect))]
    public partial bool ServiceConnected { get; set; } = false;

    private async Task CycleCheckServiceStatusAsync()
    {
        var cts = new CancellationTokenSource(2000);

        while (true)
        {
            cts.TryReset();
            await CheckServiceStatusAsync(cts.Token);
            await Task.Delay(10000);
        }
    }

    private async Task CheckServiceStatusAsync(CancellationToken ct)
    {
        var message = JsonSerializer.Serialize(new IpcRequest { Cmd = IpcCommands.CheckServiceStatus });
        try
        {
            await pipeClient.SendCommandAsync(message, ct);
            await dispatcher.InvokeAsync(() => ServiceConnected = true);
            return;
        }
        catch (TaskCanceledException)
        {
            await dispatcher.InvokeAsync(() => ShowDisplayMessage($"服务连接超时！无法连接到服务。"));
            await dispatcher.InvokeAsync(() => ServiceConnected = false);
        }
        catch (Exception ex)
        {
            await dispatcher.InvokeAsync(() => ShowDisplayMessage($"服务连接失败！错误信息：{ex}"));
            await dispatcher.InvokeAsync(() => ServiceConnected = false);
            connectCts?.Cancel();
        }
    }

    [ObservableProperty]
    public partial ObservableCollection<NetworkItem> Networks { get; set; } = [];

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
    public partial string ActiveNetworkAdapterNames { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveNetworkNames { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActivePppoeConnectionEntryName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial NetworkItem? SelectedNetwork { get; set; } = null;

    [ObservableProperty]
    public partial int SelectedNetworkIndex { get; set; } = -1;

    public bool CanModifySelectedNetworkIndex
    {
        get => ServiceConnected && !IsLoading && !IsSavingSelectedNetworkIndex;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifySelectedNetworkIndex))]
    public partial bool IsSavingSelectedNetworkIndex { get; set; } = false;

    private bool selectedNetworkIndexInitialized = false;

    async partial void OnSelectedNetworkIndexChanged(int oldValue, int newValue)
    {
        if (!selectedNetworkIndexInitialized)
        {
            selectedNetworkIndexInitialized = true;
            return;
        }

        if (IsSavingSelectedNetworkIndex)
            return;

        if (newValue < 0)
            return;

        IsSavingSelectedNetworkIndex = true;

        try
        {
            if (SelectedNetwork is not null && oldValue == -1)
            {
                var tcs = new TaskCompletionSource();
                StrongReferenceMessenger.Default.Send<ChangeNetworkConfirmMessage>(new ChangeNetworkConfirmMessage(tcs));

                await tcs.Task.WaitAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            SelectedNetworkIndex = oldValue;
            return;
        }
        finally
        {
            IsSavingSelectedNetworkIndex = false;
        }

        SelectedNetwork = Networks[(int)newValue];
        await settings.SelectedNetwork.SetValueAsync(SelectedNetwork);
        await settings.HasSavedNetworkAuth.SetValueAsync(true);

        OnPropertyChanged(nameof(SelectedNetwork));
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    public partial bool IsConnecting { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyPropertyChangedFor(nameof(CanModifySelectedNetworkIndex))]
    [NotifyPropertyChangedFor(nameof(CanModifyCredentials))]
    [NotifyPropertyChangedFor(nameof(CanModifyAutoConnectOnStartup))]
    [NotifyPropertyChangedFor(nameof(CanModifyReconnectOnWake))]
    [NotifyPropertyChangedFor(nameof(CanModifyReconnectOnDisconnect))]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial string DisplayMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AutoConnectOnStartup { get; set; } = false;

    public bool CanModifyAutoConnectOnStartup
    {
        get => ServiceConnected && !IsLoading && !IsSavingAutoConnectOnStartup;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifyAutoConnectOnStartup))]
    public partial bool IsSavingAutoConnectOnStartup { get; set; } = false; 

    async partial void OnAutoConnectOnStartupChanged(bool oldValue, bool newValue)
    {
        if (IsSavingAutoConnectOnStartup)
            return;

        IsSavingAutoConnectOnStartup = true;

        try
        {
            var policy = await settings.AutoConnectPolicy.GetValueAsync();
            policy.AutoConnectOnStartup = newValue;
            await settings.AutoConnectPolicy.SaveAsync();
        }
        catch (Exception ex)
        {
            ShowDisplayMessage($"保存设置项时失败：{ex}");
            AutoConnectOnStartup = oldValue;
        }
        finally
        {
            IsSavingAutoConnectOnStartup = false;
        }
    }

    [ObservableProperty]
    public partial bool ReconnectOnWake { get; set; } = false;

    public bool CanModifyReconnectOnWake
    {
        get => ServiceConnected && !IsLoading && !IsSavingReconnectOnWake;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifyReconnectOnWake))]
    public partial bool IsSavingReconnectOnWake { get; set; } = false;

    async partial void OnReconnectOnWakeChanged(bool oldValue, bool newValue)
    {
        if (IsSavingReconnectOnWake)
            return;

        IsSavingReconnectOnWake = true;

        try
        {
            var policy = await settings.AutoConnectPolicy.GetValueAsync();
            policy.ReconnectOnWake = newValue;
            await settings.AutoConnectPolicy.SaveAsync();
        }
        catch (Exception ex)
        {
            ShowDisplayMessage($"保存设置项时失败：{ex}");
            ReconnectOnWake = oldValue;
        }
        finally
        {
            IsSavingReconnectOnWake = false;
        }
    }

    [ObservableProperty]
    public partial bool ReconnectOnDisconnect { get; set; } = false;

    public bool CanModifyReconnectOnDisconnect
    {
        get => ServiceConnected && !IsLoading && !IsSavingReconnectOnDisconnect;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifyReconnectOnDisconnect))]
    public partial bool IsSavingReconnectOnDisconnect { get; set; } = false;

    async partial void OnReconnectOnDisconnectChanged(bool oldValue, bool newValue)
    {
        if (IsSavingReconnectOnDisconnect)
            return;

        IsSavingReconnectOnDisconnect = true;

        try
        {
            var policy = await settings.AutoConnectPolicy.GetValueAsync();
            policy.ReconnectOnDisconnect = newValue;
            await settings.AutoConnectPolicy.SaveAsync();
        }
        catch (Exception ex)
        {
            ShowDisplayMessage($"保存设置项时失败：{ex}");
            ReconnectOnDisconnect = oldValue;
        }
        finally
        {
            IsSavingReconnectOnDisconnect = false;
        }
    }

    private async Task InitializeAsync()
    {
        if (initialized)
            return;

        initialized = true;

        IsLoading = true;

        await UpdateNetworkStatusAsync();

        await dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var cts = new CancellationTokenSource(2000);
                await CheckServiceStatusAsync(cts.Token);

                if (ServiceConnected)
                {
                    bool hasNetworkAuth = false;

                    try
                    {
                        hasNetworkAuth = await settings.HasSavedNetworkAuth.GetValueAsync();
                    }
                    catch (Exception ex)
                    {
                        ShowDisplayMessage("读取配置时发生错误：" + ex);
                    }

                    if (hasNetworkAuth)
                    {
                        try
                        {
                            SelectedNetwork = await settings.SelectedNetwork.GetValueAsync();
                        }
                        catch (Exception ex)
                        {
                            ShowDisplayMessage("读取凭据时发生错误：" + ex);
                        }
                        try
                        {
                            SelectedNetwork = await settings.SelectedNetwork.GetValueAsync();
                        }
                        catch (Exception ex)
                        {
                            ShowDisplayMessage("读取保存的网络时发生错误：" + ex);
                        }
                    }

                    try
                    {
                        var autoConnect = await settings.AutoConnectPolicy.GetValueAsync();

                        // 这三个开关属于服务侧配置，读写都走共享文件
                        AutoConnectOnStartup = autoConnect.AutoConnectOnStartup;
                        ReconnectOnWake = autoConnect.ReconnectOnWake;
                        ReconnectOnDisconnect = autoConnect.ReconnectOnDisconnect;
                    }
                    catch (Exception ex)
                    {
                        ShowDisplayMessage("读取自动连接配置时发生错误：" + ex);
                    }

                    var item = Networks.FirstOrDefault(c => c.Id == SelectedNetwork?.Id);

                    if (item is not null)
                        SelectedNetworkIndex = Networks.IndexOf(item);
                }
                else
                {
                    ShowDisplayMessage("当前服务未连接。");
                }
            }
            finally
            {
                IsLoading = false;
                _ = CycleCheckServiceStatusAsync();
            }
        });
    }

    private async Task UpdateNetworkStatusAsync()
    {
        var status = await coreManager.GetNetworkStatusAsync();
        List<NetworkItem> ethernetNetworks = status.ActiveEthernetNetworks;

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
                network.IsSelected = network.Name == SelectedNetwork?.Name;
            }

            HasAvailableNetworks = Networks.Count > 0;

            IsInternetAccess = status.IsInternetAccess;
            ActiveNetworkAdapterNames = status.ActiveNetworkAdapterNames;
            ActiveNetworkNames = status.ActiveNetworkNames;

            IsEthernetNetworkAdapterConnected = status.IsEthernetConnected;
            IsOtherNetworkAdapterConnected = !IsEthernetNetworkAdapterConnected && status.NetworkConnectivityLevel != NetworkConnectivityLevel.None;
            IsNetworkConnectionManagedByOutside = status.IsNetworkConnectionManagedByOutside;

            IsPppoeNetworkConnected = status.IsPppoeNetworkConnected;
            ActivePppoeConnectionEntryName = status.ActivePppoeConnectionEntryName;
            IsPppoeConnectionManagedByOutside = status.IsPppoeConnectionManagedByOutside;
            IsNotPppoeNetworkConnection = status.IsNotPppoeNetworkConnection;

            if (SelectedNetwork is null)
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

    public bool CanModifyCredentials
    {
        get => ServiceConnected && !IsLoading;
    }

    private bool CanConnect()
    {
        if (!ServiceConnected)
            return false;

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

        if (IsConnecting)
            return false;

        // 没有匹配的逻辑网络
        if (!Networks.Any(it => it.Id == SelectedNetwork?.Id))
        {
            ShowDisplayMessage("在网络列表中没有找到目标网络连接项。");
            return false;
        }

        return true;
    }

    private bool CanDisconnect()
    {
        if (!ServiceConnected)
            return false;

        if (IsLoading)
            return false;

        // 以太网未连接
        if (!IsEthernetNetworkAdapterConnected)
            return false;

        // pppoe 网络未连接
        if (!IsPppoeNetworkConnected)
            return false;

        if (IsConnecting)
            return true;

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsConnecting = true;

        ResetCts(ref connectCts);

        try
        {
            await ExcuteConnectAsync();
        }
        catch (RasConnectException ex)
        {
            ShowDisplayMessage(ex);
        }
        catch (OperationCanceledException)
        {
            ShowDisplayMessage("连接请求被中断。");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        if (IsConnecting)
        {
            CancelCts(ref connectCts);
            return;
        }

        try
        {
            await ExcuteDisconnectAsync();
        }
        catch (RasConnectException ex)
        {
            ShowDisplayMessage($"断开连接请求失败: {ex}");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private bool CanResetConnection()
    {
        if (!ServiceConnected)
            return false;

        if (IsLoading)
            return false;

        return true;
    }

    [ObservableProperty]
    public partial bool IsResettingRasConnection { get; set; } = false;

    [RelayCommand(CanExecute = nameof(CanResetConnection))]
    private async Task ResetRasConnectionAsync()
    {
        IsResettingRasConnection = true;

        try
        {
            await ExcuteDisconnectAsync();
            await ExcuteConnectAsync();
        }
        catch (RasConnectException ex)
        {
            ShowDisplayMessage($"重置连接时失败：{ex}");
        }
        finally
        {
            IsResettingRasConnection = false;
        }
    }

    private async Task ExcuteConnectAsync()
    {
        string request = JsonSerializer.Serialize(new IpcRequest() { Cmd = IpcCommands.RasConnect });
        string raw = await pipeClient.SendCommandAsync(request, connectCts!.Token);

        var response = IpcUtils.ParseResponse(raw);

        if (!response.Success)
            throw new RasConnectException($"连接请求失败: {response.Error}");

        await settings.HasSavedNetworkAuth.SetValueAsync(true);

        if (SelectedNetwork is not null)
            await settings.SelectedNetwork.SetValueAsync(SelectedNetwork);
    }

    private async Task ExcuteDisconnectAsync()
    {
        string request = JsonSerializer.Serialize(new IpcRequest() { Cmd = IpcCommands.RasDisconnect });
        string raw = await pipeClient.SendCommandAsync(request);

        var response = IpcUtils.ParseResponse(raw);

        if (!response.Success)
            throw new RasConnectException($"断开连接请求失败: {response.Error}");
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

public class RasConnectException : Exception
{
    public RasConnectException() : base() { }
    public RasConnectException(string message) : base(message) { }
}