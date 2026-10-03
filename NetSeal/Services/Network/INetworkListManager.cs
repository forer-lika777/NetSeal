// See https://github.com/mirror/mingw-w64/blob/master/mingw-w64-headers/include/netlistmgr.h

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NetSeal.Services.Network;

[ComImport, Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INetworkListManager
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
    void GetConnectivity();
}

[ComImport, Guid("DCB00002-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INetwork
{
    void GetTypeInfoCount();
    void GetTypeInfo();
    void GetIDsOfNames();
    void Invoke();

    string GetName();
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
public interface IEnumNetworks
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
public interface INetworkConnection
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
public interface IEnumNetworkConnections
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
public enum NLM_ENUM_NETWORK
{
    NLM_ENUM_NETWORK_CONNECTED = 0x1,
    NLM_ENUM_NETWORK_DISCONNECTED = 0x2,
    NLM_ENUM_NETWORK_ALL = 0x3,
}

[Flags]
public enum NLM_CONNECTIVITY
{
    NLM_CONNECTIVITY_DISCONNECTED = 0x0,
    NLM_CONNECTIVITY_IPV4_NOTRAFFIC = 0x1,
    NLM_CONNECTIVITY_IPV6_NOTRAFFIC = 0x2,
    NLM_CONNECTIVITY_IPV4_SUBNET = 0x10,
    NLM_CONNECTIVITY_IPV4_LOCALNETWORK = 0x20,
    NLM_CONNECTIVITY_IPV4_INTERNET = 0x40,
    NLM_CONNECTIVITY_IPV6_SUBNET = 0x100,
    NLM_CONNECTIVITY_IPV6_LOCALNETWORK = 0x200,
    NLM_CONNECTIVITY_IPV6_INTERNET = 0x400,
}

public enum NLM_NETWORK_CATEGORY
{
    NLM_NETWORK_CATEGORY_PUBLIC = 0x0,
    NLM_NETWORK_CATEGORY_PRIVATE = 0x1,
    NLM_NETWORK_CATEGORY_DOMAIN_AUTHENTICATED = 0x2,
}
