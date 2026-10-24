// See https://github.com/mirror/mingw-w64/blob/master/mingw-w64-headers/include/netlistmgr.h

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NetSeal.Core.Services.Network;

[ComImport, Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B")]
internal class NetworkListManagerClass
{
}

[ComImport, Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INetworkListManager
{
    void GetTypeInfoCount();
    void GetTypeInfo();
    void GetIDsOfNames();
    void Invoke();

    [return: MarshalAs(UnmanagedType.Interface)]
    IEnumNetworks GetNetworks(NLM_ENUM_NETWORK flags);
    [return: MarshalAs(UnmanagedType.Interface)]
    INetwork GetNetwork(Guid networkId);
    [return: MarshalAs(UnmanagedType.Interface)]
    IEnumNetworkConnections GetNetworkConnections();
    void GetNetworkConnection(Guid networkConnectionId);
    bool IsConnectedToInternet { get; }
    bool IsConnected { get; }
    NLM_CONNECTIVITY GetConnectivity();
}

[ComImport, Guid("DCB00002-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INetwork
{
    void GetTypeInfoCount();
    void GetTypeInfo();
    void GetIDsOfNames();
    void Invoke();

    string? GetName();
    void SetName();
    string GetDescription();
    void SetDescription();
    Guid GetNetworkId();
    void GetDomainType();
    IEnumNetworkConnections GetNetworkConnections();
    void GetTimeCreatedAndConnected();
    bool IsConnectedToInternet { get; }
    bool IsConnected { get; }
    void GetConnectivity();
    NLM_NETWORK_CATEGORY GetCategory();
    void SetCategory();    // 占位
}

[ComImport, Guid("DCB00003-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumNetworks
{
    void GetTypeInfoCount();
    void GetTypeInfo();
    void GetIDsOfNames();
    void Invoke();

    [return: MarshalAs(UnmanagedType.Interface)]
    object NewEnum { get; }

    [PreserveSig]
    int Next(
        uint celt,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] rgelt,
        out uint pceltFetched);

    void Skip(uint celt);

    void Reset();

    void Clone(out IEnumNetworks ppEnumNetwork);
}

[ComImport, Guid("DCB00005-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INetworkConnection
{
    void GetTypeInfoCount();
    void GetTypeInfo();
    void GetIDsOfNames();
    void Invoke();

    INetwork GetNetwork();
    bool IsConnectedToInternet { get; }
    bool IsConnected { get; }
    void GetConnectivity();
    Guid GetConnectionId();
    Guid GetAdapterId();
    void GetDomainType();
}

[ComImport, Guid("DCB00006-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumNetworkConnections
{
    void GetTypeInfoCount();
    void GetTypeInfo();
    void GetIDsOfNames();
    void Invoke();

    [return: MarshalAs(UnmanagedType.Interface)]
    object NewEnum { get; }

    [PreserveSig]
    int Next(
        uint celt,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] rgelt,
        out uint pceltFetched);

    void Skip(uint celt);

    void Reset();

    void Clone(out IEnumNetworkConnections ppEnumNetworkConnection);
}

[Flags]
internal enum NLM_ENUM_NETWORK
{
    /// <summary>
    /// 返回已连接的网络。
    /// </summary>
    NLM_ENUM_NETWORK_CONNECTED = 0x1,

    /// <summary>
    /// 返回未连接的网络。
    /// </summary>
    NLM_ENUM_NETWORK_DISCONNECTED = 0x2,

    /// <summary>
    /// 返回已连接和未连接的网络。
    /// </summary>
    NLM_ENUM_NETWORK_ALL = 0x3,
}

[Flags]
internal enum NLM_CONNECTIVITY
{
    /// <summary>
    /// 基础网络接口未连接到任何网络。
    /// </summary>
    NLM_CONNECTIVITY_DISCONNECTED = 0x0,

    /// <summary>
    /// 已连接到网络，但服务无法检测任何 IPv4 网络流量。
    /// </summary>
    NLM_CONNECTIVITY_IPV4_NOTRAFFIC = 0x1,

    /// <summary>
    /// 已连接到网络，但服务无法检测任何 IPv6 网络流量。
    /// </summary>
    NLM_CONNECTIVITY_IPV6_NOTRAFFIC = 0x2,

    /// <summary>
    /// 使用 IPv4 协议连接到本地子网。
    /// </summary>
    NLM_CONNECTIVITY_IPV4_SUBNET = 0x10,

    /// <summary>
    /// 使用 IPv4 协议连接到路由网络。
    /// </summary>
    NLM_CONNECTIVITY_IPV4_LOCALNETWORK = 0x20,

    /// <summary>
    /// 使用 IPv4 协议连接到 Internet。
    /// </summary>
    NLM_CONNECTIVITY_IPV4_INTERNET = 0x40,

    /// <summary>
    /// 使用 IPv6 协议连接到本地子网。
    /// </summary>
    NLM_CONNECTIVITY_IPV6_SUBNET = 0x100,

    /// <summary>
    /// 使用 IPv6 协议连接到本地网络。
    /// </summary>
    NLM_CONNECTIVITY_IPV6_LOCALNETWORK = 0x200,

    /// <summary>
    /// 使用 IPv6 协议连接到 Internet。
    /// </summary>
    NLM_CONNECTIVITY_IPV6_INTERNET = 0x400,
}

internal enum NLM_NETWORK_CATEGORY
{
    /// <summary>
    /// 该网络是不受信任的公共网络。
    /// </summary>
    NLM_NETWORK_CATEGORY_PUBLIC = 0x0,

    /// <summary>
    /// 该网络是受信任的专用网络。
    /// </summary>
    NLM_NETWORK_CATEGORY_PRIVATE = 0x1,

    /// <summary>
    /// 针对 Active Directory 域对网络进行身份验证。
    /// </summary>
    NLM_NETWORK_CATEGORY_DOMAIN_AUTHENTICATED = 0x2,
}
