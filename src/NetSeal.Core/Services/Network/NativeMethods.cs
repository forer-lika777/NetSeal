// See https://github.com/mirror/mingw-w64/blob/master/mingw-w64-headers/include/netlistmgr.h

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace NetSeal.Core.Services.Network;

[GeneratedComInterface]
[Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal partial interface INetworkListManager
{
    void GetTypeInfoCount(nint pctinfo);
    void GetTypeInfo(int iTInfo, int lcid, nint ppTInfo);
    void GetIDsOfNames(nint riid, nint rgszNames, int cNames, int lcid, nint rgDispId);
    void Invoke(int dispIdMember, nint riid, int lcid, short wFlags, nint pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr);

    void GetNetworks(NLM_ENUM_NETWORK Flags, out IEnumNetworks ppEnumNetworks);
    void GetNetwork(Guid gdNetworkId, out INetwork ppNetwork);
    void GetNetworkConnections(out IEnumNetworkConnections ppEnum);
    void GetNetworkConnection(Guid gdNetworkConnectionId, out INetworkConnection ppNetworkConnection);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetIsConnectedToInternet();
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetIsConnected();
    void GetConnectivity(out NLM_CONNECTIVITY pConnectivity);
}

[GeneratedComInterface]
[Guid("DCB00002-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe partial interface INetwork
{
    void GetTypeInfoCount(nint pctinfo);
    void GetTypeInfo(int iTInfo, int lcid, nint ppTInfo);
    void GetIDsOfNames(nint riid, nint rgszNames, int cNames, int lcid, nint rgDispId);
    void Invoke(int dispIdMember, nint riid, int lcid, short wFlags, nint pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr);

    void GetName(out nint pszNetworkName);
    void SetName(nint szNetworkNewName);
    nint GetDescription();
    void SetDescription(nint szDescription);
    void GetNetworkId(nint pgdGuidNetworkId);
    void GetDomainType(out NLM_DOMAIN_TYPE pNetworkType);
    void GetNetworkConnections(out IEnumNetworkConnections ppEnumNetworkConnection);
    void GetTimeCreatedAndConnected(
        out uint pdwLowDateTimeCreated,
        out uint pdwHighDateTimeCreated,
        out uint pdwLowDateTimeConnected,
        out uint pdwHighDateTimeConnected);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetIsConnectedToInternet();
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetIsConnected();
    void GetConnectivity(out NLM_CONNECTIVITY pConnectivity);
    void GetCategory(out NLM_NETWORK_CATEGORY pCategory);
    void SetCategory(NLM_NETWORK_CATEGORY NewCategory);
}

[GeneratedComInterface]
[Guid("DCB00003-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IEnumNetworks
{
    void GetTypeInfoCount(nint pctinfo);
    void GetTypeInfo(int iTInfo, int lcid, nint ppTInfo);
    void GetIDsOfNames(nint riid, nint rgszNames, int cNames, int lcid, nint rgDispId);
    void Invoke(int dispIdMember, nint riid, int lcid, short wFlags, nint pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr);

    nint get_NewEnum();

    [PreserveSig]
    int Next(
        uint celt,
        nint rgelt,
        out uint pceltFetched);

    void Skip(uint celt);

    void Reset();

    void Clone(out IEnumNetworks ppEnumNetwork);
}

[GeneratedComInterface]
[Guid("DCB00005-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface INetworkConnection
{
    void GetTypeInfoCount(nint pctinfo);
    void GetTypeInfo(int iTInfo, int lcid, nint ppTInfo);
    void GetIDsOfNames(nint riid, nint rgszNames, int cNames, int lcid, nint rgDispId);
    void Invoke(int dispIdMember, nint riid, int lcid, short wFlags, nint pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr);

    void GetNetwork(out INetwork network);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetIsConnectedToInternet();
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetIsConnected();
    void GetConnectivity(out NLM_CONNECTIVITY pConnectivity);
    Guid GetConnectionId();
    Guid GetAdapterId();
    void GetDomainType(out NLM_DOMAIN_TYPE pDomainType);
}

[GeneratedComInterface]
[Guid("DCB00006-570F-4A9B-8D69-199FDBA5723B")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal partial interface IEnumNetworkConnections
{
    void GetTypeInfoCount(nint pctinfo);
    void GetTypeInfo(int iTInfo, int lcid, nint ppTInfo);
    void GetIDsOfNames(nint riid, nint rgszNames, int cNames, int lcid, nint rgDispId);
    void Invoke(int dispIdMember, nint riid, int lcid, short wFlags, nint pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr);

    nint get_NewEnum();

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

internal enum NLM_DOMAIN_TYPE
{
    NLM_DOMAIN_TYPE_NON_DOMAIN_NETWORK = 0x0,
    NLM_DOMAIN_TYPE_DOMAIN_NETWORK = 0x1,
    NLM_DOMAIN_TYPE_DOMAIN_AUTHENTICATED = 0x2
}