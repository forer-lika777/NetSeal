using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetSeal.Core.Models;
using NetSeal.Core.Services;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using NetSeal.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.PointOfService;
using Windows.Media.DialProtocol;
using Windows.System;

namespace NetSeal.ViewModels;

public partial class StatusPageModel : ObservableObject, IRecipient<NetworkInformationChangedMessage>
{
    private readonly IAppSettings settings;
    private readonly IUiDispatcher dispatcher;
    private readonly INetworkService networkService;
    private readonly INetworkAdapterService adapterService;
    private readonly ICoreManager coreManager;
    private readonly IRasDialer rasDialer;

    private DateTime lastHandled = DateTime.MinValue;
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(2);
    private readonly Lock locker = new();

    private CancellationTokenSource? connectCts = null;

    private bool initialized = false;

    public StatusPageModel(
        IAppSettings appSettings, 
        IUiDispatcher dispatcher, 
        INetworkService networkService, 
        INetworkAdapterService adapterService, 
        IRasDialer rasDialer, 
        ICoreManager coreManager)
    {
        this.settings = appSettings;
        this.dispatcher = dispatcher;
        this.networkService = networkService;
        this.adapterService = adapterService;
        this.rasDialer = rasDialer;
        this.coreManager = coreManager;
        StrongReferenceMessenger.Default.Register<NetworkInformationChangedMessage>(this);
        _ = InitializeAsync();
        Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
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
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial int SelectedNetworkIndex { get; set; } = -1;

    async partial void OnSelectedNetworkIndexChanged(int oldValue, int newValue)
    {
        if (newValue < 0)
            return;

        if (SelectedNetwork is not null && oldValue == -1)
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

    [ObservableProperty]
    public partial bool AutoConnectOnStartup { get; set; } = false;

    [ObservableProperty]
    public partial bool IsSavingAutoConnectOnStartup { get; set; } = false;

    async partial void OnAutoConnectOnStartupChanging(bool oldValue, bool newValue)
    {
        if (oldValue)
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

    [ObservableProperty]
    public partial bool IsSavingReconnectOnWake { get; set; } = false;

    async partial void OnReconnectOnWakeChanged(bool oldValue, bool newValue)
    {
        if (oldValue)
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

    [ObservableProperty]
    public partial bool IsSavingReconnectOnDisconnect { get; set; } = false;

    async partial void OnReconnectOnDisconnectChanged(bool oldValue, bool newValue)
    {
        if (oldValue)
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
                    var credentials = await settings.RasCredentials.GetValueAsync();
                    AccountId = credentials.Username;
                    Password = credentials.Password;
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

            IsLoading = false;
        });
    }

    public void Receive(NetworkInformationChangedMessage message)
    {
        NetworkInformation_NetworkStatusChanged(this);
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

        await UpdateNetworkStatusAsync();

        await InitializeAsync();
    }

    private async Task UpdateNetworkStatusAsync()
    {
        var status = await coreManager.GetNetworkStatusAsync();
        List<NetworkItem> ethernetNetworks = status.ActiveEthernetNetworks;

        bool hasSavedNetworkAuth = false;
        try
        {
            hasSavedNetworkAuth = await settings.HasSavedNetworkAuth.GetValueAsync();
        }
        catch (Exception ex)
        {
            ShowDisplayMessage("更新网络连接状态时发生错误：" + ex);
        }

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

            if (!hasSavedNetworkAuth)
            {
                ShowDisplayMessage("在网络接口列表中选择要连接的网络。");
                return;
            }

            if (!HasAvailableNetworks)
            {
                ShowDisplayMessage("未找到可用的网络连接。");
                return;
            }

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

        if (IsConnecting)
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

    [RelayCommand]
    private async Task SetCredentialsAsync()
    {
        await rasDialer.RasSetCredentialsAsync(null, RasEntryName.NetSealServiceEntryName, AccountId, Password);
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsConnecting = true;

        //var credObj = new { user = AccountId, password = Password };
        //string tempFile = Path.Combine(Path.GetTempPath(), $"netseal_cred_{Guid.NewGuid():N}.json");
        //File.WriteAllBytes(tempFile, JsonSerializer.SerializeToUtf8Bytes(credObj));

        //string agentPath = ResolveAgentPath();

        //var psi = new ProcessStartInfo
        //{
        //    FileName = agentPath,
        //    Arguments = $"--set-cred \"{tempFile}\"",
        //    UseShellExecute = true,
        //    Verb = "runas"
        //};

        //try
        //{
        //    var process = Process.Start(psi);
        //    process?.WaitForExit();

        //    if (process?.ExitCode == 0)
        //    {
        //        ShowDisplayMessage("凭据保存成功。");
        //    }
        //}
        //catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        //{
        //    // 用户点了取消 UAC
        //    File.Delete(tempFile);
        //    // UI 提示“已取消保存”
        //}
        //catch (Exception ex)
        //{
        //    File.Delete(tempFile);
        //    ShowDisplayMessage("凭据保存失败。");
        //}

        await rasDialer.RasCreateEntryAsync(null, RasEntryName.NetSealServiceEntryName, AccountId, Password);

        await ExcuteConnectTaskAsync();

        IsConnecting = false;
        IsPppoeNetworkConnected = true;
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
        IsPppoeNetworkConnected = false;
    }

    private async Task ExcuteConnectTaskAsync()
    {
        ResetCts(ref connectCts);

        try
        {
            await rasDialer.RasConnectAsync(null, RasEntryName.NetSealServiceEntryName, AccountId, Password, connectCts!.Token);

            await settings.HasSavedNetworkAuth.SetValueAsync(true);
            //appSettings.AccountId.set
            //appSettings.Password.Value = Password;

            if (SelectedNetwork is not null)
                await settings.SelectedNetwork.SetValueAsync(SelectedNetwork);
        }
        catch (Win32Exception ex)
        {
            await dispatcher.InvokeAsync(() => ShowDisplayMessage(ex));
        }
    }

    private async Task ExcuteDisconnectTaskAsync()
    {
        try
        {
            await rasDialer.RasDisconnectAsync(null, RasEntryName.NetSealServiceEntryName);
        }
        catch (Win32Exception ex)
        {
            await dispatcher.InvokeAsync(() => ShowDisplayMessage(ex));
        }
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

    /// <summary>
    /// 定位 NetSeal.Agent.exe。
    /// <para>安装后布局：&lt;安装目录&gt;\Agent\NetSeal.Agent.exe</para>
    /// <para>开发期布局：仓库根目录下的 NetSeal.Agent\bin\&lt;配置&gt;\net10.0\NetSeal.Agent.exe</para>
    /// </summary>
    private static string ResolveAgentPath()
    {
        string installed = Path.Combine(AppContext.BaseDirectory, "Agent", "NetSeal.Agent.exe");
        if (File.Exists(installed))
            return installed;

        // 开发期：从输出目录向上回溯，寻找仓库中的 NetSeal.Agent 项目输出
        string? dir = AppContext.BaseDirectory;
        for (int depth = 0; depth < 6 && !string.IsNullOrEmpty(dir); depth++)
        {
            foreach (var configuration in new[] { "Debug", "Release" })
            {
                string candidate = Path.Combine(dir, "NetSeal.Agent", "bin", configuration, "net10.0", "NetSeal.Agent.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        // 找不到时返回安装布局路径，让调用方得到明确的“文件不存在”错误
        return installed;
    }
}
