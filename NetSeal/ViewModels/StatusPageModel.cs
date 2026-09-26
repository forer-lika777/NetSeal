using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSeal.Models;
using NetSeal.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace NetSeal.ViewModels;

public partial class StatusPageModel : ObservableObject
{
    private readonly IAppSettings appSettings;

    private IntPtr? connectPtr = null;

    [ObservableProperty]
    public partial ObservableCollection<InterfaceNameDisplayStatus> Interfaces { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool HasAvailableInterfaces { get; set; } = false;

    [ObservableProperty]
    public partial string SelectedInterfaceName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SelectedInterfaceIndex { get; set; } = -1;

    partial void OnSelectedInterfaceIndexChanged(int value)
    {
        if (value >= 0)
            SelectedInterfaceName = Interfaces[(int)value].Name;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool IsConnected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsConnecting { get; set; } = false;

    [ObservableProperty]
    public partial string DisplayMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string AccountId { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool EthernetCablePluggedIn { get; set; } = 
        NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
        .Any(n =>
        {
            var desc = n.Description;
            // 排除虚拟/蓝牙
            if (desc.Contains("Radmin", StringComparison.OrdinalIgnoreCase))
                return false;
            if (desc.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
                return false;
            if (desc.Contains("VPN", StringComparison.OrdinalIgnoreCase))
                return false;
            if (desc.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                return false;
            if (desc.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase))
                return false;
            if (desc.Contains("VMware", StringComparison.OrdinalIgnoreCase))
                return false;
            if (desc.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase))
                return false;

            // 真实物理网卡，链路 Up
            return n.OperationalStatus == OperationalStatus.Up;
        });

    public static IReadOnlyList<NetworkInterface> GetAllNics()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n =>
                n.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                n.OperationalStatus == OperationalStatus.Up &&
                !n.Name.Contains("-WFP", StringComparison.OrdinalIgnoreCase) &&
                !n.Name.Contains("-Npcap", StringComparison.OrdinalIgnoreCase) &&
                !n.Name.Contains("-QoS", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public StatusPageModel(IAppSettings appSettings)
    {
        this.appSettings = appSettings;
        Init();
    }

    private void Init()
    {
        Interfaces.CollectionChanged += (s, e) => HasAvailableInterfaces = Interfaces.Count > 0;

        UpdateStatus();

        if (appSettings.HasSavedConnectionAuth.Value)
        {
            AccountId = appSettings.AccountId.Value;
            Password = appSettings.Password.Value;
            SelectedInterfaceName = appSettings.SelectedConnectionName.Value;

            var item = Interfaces.FirstOrDefault(c => c.Name == appSettings.SelectedConnectionName.Value);

            _ = Connect();
        }

        if (SelectedInterfaceIndex >= 0)
            SelectedInterfaceName = Interfaces[(int)SelectedInterfaceIndex].Name;
    }

    private void UpdateStatus()
    {
        var interfaces = GetAllNics();

        System.Diagnostics.Debug.WriteLine("=== GetAllNics 结果 ===");
        foreach (var it in interfaces)
            System.Diagnostics.Debug.WriteLine($"[{it.Name}] desc=[{it.Description}]");

        for (int i = Interfaces.Count - 1; i >= 0; i--)
        {
            if (!interfaces.Any(it => it.Name == Interfaces[i].Name))
            {
                Interfaces.RemoveAt(i);
            }
        }

        foreach (var it in interfaces)
        {
            if (!Interfaces.Any(c => c.Name == it.Name))
            {
                Interfaces.Add(new InterfaceNameDisplayStatus(it.Name));
            }
        }

        foreach (var it in Interfaces)
        {
            if (it.Name == SelectedInterfaceName)
            {
                it.Selected = true;
            }
        }

        if (!appSettings.HasSavedConnectionAuth.Value)
        {
            ShowDisplayMessage("在网络接口列表中选择要连接的网络。");
            return;
        }

        if (!EthernetCablePluggedIn)
        {
            ShowDisplayMessage("当前未接入有线网络连接。");
            return;
        }
    }

    private bool CanConnect()
    {
        if (IsConnected)
            return false;

        //if (HasAvailableConnections)
        //    return true;

        if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(Password))
            return false;

        if (!EthernetCablePluggedIn)
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task Connect()
    {
        IsConnected = RasDialer.IsConnected();

        if (IsConnected || IsConnecting)
            return;

        if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(Password))
        {
            ShowDisplayMessage("请输入用户名或密码");
            return;
        }

        //if (!Connections.Any(c => c.ConnectionName == SelectedConnectionName))
        //{
        //    ShowDisplayMessage("在连接列表中没有找到目标连接项。");
        //    return;
        //}

        try
        {
            IsConnecting = true;

            connectPtr = RasDialer.Dial(SelectedInterfaceName, AccountId, Password);

            IsConnecting = false;
            IsConnected = true;

            appSettings.HasSavedConnectionAuth.Value = true;
            appSettings.AccountId.Value = AccountId;
            appSettings.Password.Value = Password;
        }
        catch (InvalidOperationException ex)
        {
            ShowDisplayMessage(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DisConnect()
    {
        if (!IsConnected)
            return;

        if (connectPtr is null)
            return;

        try
        {
            RasDialer.HangUp((IntPtr)connectPtr);
        }
        catch (InvalidOperationException ex)
        {
            ShowDisplayMessage(ex.Message);
        }

        IsConnected = false;
    }

    private void ShowDisplayMessage(string message)
    {
        DisplayMessage = message;
    }
}
