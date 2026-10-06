//--------------------------------------------------------------------------
// <copyright file="RasType.cs" company="Jeff Winn">
//      Copyright (c) Jeff Winn. All rights reserved.
//
//      The use and distribution terms for this software is covered by the
//      GNU Library General Public License (LGPL) v2.1 which can be found
//      in the License.rtf at the root of this distribution.
//      By using this software in any fashion, you are agreeing to be bound by
//      the terms of this license.
//
//      You must not remove this notice, or any other, from this software.
// </copyright>
//--------------------------------------------------------------------------
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace NetSeal.Services.Network.Ras;

internal static class Ras
{
    public const int RASCS_PAUSED = 0x1000;
    public const int RASCS_DONE = 0x2000;
    public const int RASCSS_DONE = 0x2000;

    public const int RAS_MaxDeviceType = 16;
    public const int RAS_MaxPhoneNumber = 128;
    public const int RAS_MaxIpAddress = 15;
    public const int RAS_MaxIpxAddress = 21;

    public const int RAS_MaxEntryName = 256;
    public const int RAS_MaxDeviceName = 128;
    public const int RAS_MaxCallbackNumber = RAS_MaxPhoneNumber;
    public const int RAS_MaxAreaCode = 10;
    public const int RAS_MaxPadType = 32;
    public const int RAS_MaxX25Address = 200;
    public const int RAS_MaxFacilities = 200;
    public const int RAS_MaxUserData = 200;
    public const int RAS_MaxReplyMessage = 1024;
    public const int RAS_MaxDnsSuffix = 256;

    public const string RASDT_Modem = "modem";
    public const string RASDT_Isdn = "isdn";
    public const string RASDT_X25 = "x25";
    public const string RASDT_Vpn = "vpn";
    public const string RASDT_Pad = "pad";
    public const string RASDT_Generic = "GENERIC";
    public const string RASDT_Serial = "SERIAL";
    public const string RASDT_FrameRelay = "FRAMERELAY";
    public const string RASDT_Atm = "ATM";
    public const string RASDT_Sonet = "SONET";
    public const string RASDT_SW56 = "SW56";
    public const string RASDT_Irda = "IRDA";
    public const string RASDT_Parallel = "PARALLEL";
    public const string RASDT_PPPoE = "PPPoE";

    public enum RASTUNNELENDPOINTTYPE
    {
        Unknown,
        IPv4,
        IPv6
    }

    [Flags]
    public enum RASCF
    {
        AllUsers = 0x1,
        GlobalCreds = 0x2,
        OwnerKnown = 0x4,
        OwnerMatch = 0x8
    }

    [Flags]
    public enum RASCN
    {
        Connection = 0x1,
        Disconnection = 0x2,
        //BandwidthAdded = 0x4,
        //BandwidthRemoved = 0x8,
        //Dormant = 0x10,
        //Reconnection = 0x20,
        //EPDGPacketArrival = 0x40
    }

    [Flags]
    public enum RDEOPT
    {
        None = 0x0,
        //UsePrefixSuffix = 0x1,
        PausedStates = 0x2,
        //IgnoreModemSpeaker = 0x4,
        //SetModemSpeaker = 0x8,
        //IgnoreSoftwareCompression = 0x10,
        //SetSoftwareCompression = 0x20,
        //DisableConnectedUI = 0x40,
        //DisableReconnectUI = 0x80,
        //DisableReconnect = 0x100,
        //NoUser = 0x200,
        //PauseOnScript = 0x400,
        //Router = 0x800,
        //CustomDial = 0x1000,
        //UseCustomScripting = 0x2000
    }

    public enum NotifierType
    {
        RasDialFunc2 = 2
    }

    [Flags]
    public enum RASCM
    {
        None = 0x0,
        UserName = 0x1,
        Password = 0x2,
        Domain = 0x4
    }
}

/// <summary>
/// Defines the Link Control Protocol (LCP) authentication protocol types.
/// </summary>
public enum RasLcpAuthenticationType
{
    /// <summary>
    /// No authentication protocol used.
    /// </summary>
    None = 0,

    /// <summary>
    /// Password Authentication Protocol.
    /// </summary>
    Pap = 0xC023,

    /// <summary>
    /// Shiva Password Authentication Protocol.
    /// </summary>
    Spap = 0xC027,

    /// <summary>
    /// Challenge Handshake Authentication Protocol.
    /// </summary>
    Chap = 0xC223,

    /// <summary>
    /// Extensible Authentication Protocol.
    /// </summary>
    Eap = 0xC227
}

public enum RasLcpAuthenticationDataType
{
    /// <summary>
    /// No authentication data used.
    /// </summary>
    None = 0,

    /// <summary>
    /// MD5 Challenge Handshake Authentication Protocol.
    /// </summary>
    MD5Chap = 0x05,

    /// <summary>
    /// Challenge Handshake Authentication Protocol.
    /// </summary>
    MSChap = 0x80,

    /// <summary>
    /// Challenge Handshake Authentication Protocol version 2.
    /// </summary>
    MSChap2 = 0x81
}

public enum RasCompressionType
{
    /// <summary>
    /// No compression in use.
    /// </summary>
    None = 0x0,

    /// <summary>
    /// STAC option 4.
    /// </summary>
    Stac = 0x5,

    /// <summary>
    /// Microsoft Point-to-Point Compression (MPPC) protocol.
    /// </summary>
    Mppc = 0x6
}

public enum RasPhoneBookType
{
    /// <summary>
    /// The phone book is in the user's profile.
    /// </summary>
    User = 0,

    /// <summary>
    /// The phone book is a system phone book and is in the All Users profile.
    /// </summary>
    AllUsers = 1
}

public enum RasDialMode
{
    /// <summary>
    /// No dial mode specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Dial all subentries.
    /// </summary>
    DialAll = 1,

    /// <summary>
    /// Dial the number of subentries as additional bandwidth is needed.
    /// </summary>
    DialAsNeeded = 2
}

public enum RasEntryType
{
    /// <summary>
    /// No entry type specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Phone line.
    /// </summary>
    Phone = 1,

    /// <summary>
    /// Virtual Private Network.
    /// </summary>
    Vpn = 2,

    /// <summary>
    /// Direct serial or parallel connection.
    /// <para>
    /// <b>Windows Vista or later:</b> This value is no longer supported.
    /// </para>
    /// </summary>
    Direct = 3,

    /// <summary>
    /// Connection Manager (CM) connection.
    /// <para>
    /// <b>Note:</b> This member is reserved for system use only.
    /// </para>
    /// </summary>
    Internet = 4,

    /// <summary>
    /// Broadband connection.
    /// <para>
    /// <b>Windows XP or later:</b> This value is supported.
    /// </para>
    /// </summary>
    Broadband = 5
}

public enum RasEncryptionType
{
    /// <summary>
    /// No encryption type specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Require encryption.
    /// </summary>
    Require = 1,

    /// <summary>
    /// Require maximum encryption.
    /// </summary>
    RequireMax = 2,

    /// <summary>
    /// Use encryption if available.
    /// </summary>
    Optional = 3
}

public enum RasIsolationState
{
    /// <summary>
    /// The connection isolation state is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The connection isolation state is not restricted.
    /// </summary>
    NotRestricted,

    /// <summary>
    /// The connection isolation state is in probation.
    /// </summary>
    InProbation,

    /// <summary>
    /// The connection isolation state is restricted access.
    /// </summary>
    RestrictedAccess
}

public enum RasFramingProtocol
{
    /// <summary>
    /// No framing protocol specified.
    /// </summary>
    None = 0x0,

    /// <summary>
    /// Point-to-point (PPP) protocol.
    /// </summary>
    Ppp = 0x1,

    /// <summary>
    /// Serial Line Internet Protocol (SLIP).
    /// </summary>
    Slip = 0x2,

    /// <summary>
    /// This member is no longer supported.
    /// </summary>
    [Obsolete("This member is no longer supported.", false)]
    Ras = 0x4
}

public enum RasIkeV2AuthenticationType
{
    /// <summary>
    /// No authentication.
    /// </summary>
    None = 0,

    /// <summary>
    /// X.509 Public Key Infrastructure Certificate.
    /// </summary>
    X509Certificate = 1,

    /// <summary>
    /// Extensible Authentication Protocol (EAP).
    /// </summary>
    Eap = 2
}

public enum RasIPSecEncryptionType
{
    /// <summary>
    /// No encryption type specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// DES encryption.
    /// </summary>
    Des = 1,

    /// <summary>
    /// Triple DES encryption.
    /// </summary>
    TripleDes = 2,

    /// <summary>
    /// AES 128-bit encryption.
    /// </summary>
    Aes128 = 3,

    /// <summary>
    /// AES 192-bit encryption.
    /// </summary>
    Aes192 = 4,

    /// <summary>
    /// AES 256-bit encryption.
    /// </summary>
    Aes256 = 5,

    /// <summary>
    /// Maximum encryption.
    /// </summary>
    Max = 6
}

public enum RasVpnStrategy
{
    /// <summary>
    /// Dials PPTP first. If PPTP fails, L2TP is attempted.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Dial PPTP only.
    /// </summary>
    PptpOnly = 1,

    /// <summary>
    /// Always dial PPTP first.
    /// </summary>
    PptpFirst = 2,

    /// <summary>
    /// Dial L2TP only.
    /// </summary>
    L2tpOnly = 3,

    /// <summary>
    /// Always dial L2TP first.
    /// </summary>
    L2tpFirst = 4,

    /// <summary>
    /// Dial SSTP only.
    /// <para>
    /// <b>Windows Vista or later:</b> This value is supported.
    /// </para>
    /// </summary>
    SstpOnly = 5,

    /// <summary>
    /// Always dial SSTP first.
    /// <para>
    /// <b>Windows Vista or later:</b> This value is supported.
    /// </para>
    /// </summary>
    SstpFirst = 6,

    /// <summary>
    /// Dial IKEv2 only.
    /// <para>
    /// <b>Windows 7 or later:</b> This value is supported.
    /// </para>
    /// </summary>
    IkeV2Only = 7,

    /// <summary>
    /// Dial IKEv2 first.
    /// <para>
    /// <b>Windows 7 or later:</b> This value is supported.
    /// </para>
    /// </summary>
    IkeV2First = 8
}

/// <summary>
/// Defines the different states available for a remote access service (RAS) connection.
/// </summary>
/// <remarks>WARNING! Do not write code that depends on the order or occurrence of a particular connection state, because this can vary between platforms.</remarks>
public enum RasConnectionState
{
    /// <summary>
    /// The communications port is about to be opened.
    /// </summary>
    OpenPort = 0,

    /// <summary>
    /// The communications port has been opened successfully.
    /// </summary>
    PortOpened,

    /// <summary>
    /// The device is about to be connected.
    /// </summary>
    ConnectDevice,

    /// <summary>
    /// The device has connected successfully.
    /// </summary>
    DeviceConnected,

    /// <summary>
    /// The devices within the device chain have all connected, a physical link has been established.
    /// </summary>
    AllDevicesConnected,

    /// <summary>
    /// The authentication process is starting.
    /// </summary>
    Authenticate,

    /// <summary>
    /// An authentication event has occurred.
    /// </summary>
    AuthNotify,

    /// <summary>
    /// The client has requested another authentication attempt.
    /// </summary>
    AuthRetry,

    /// <summary>
    /// The remote access server has requested a callback number.
    /// </summary>
    AuthCallback,

    /// <summary>
    /// The client has requested to change the password on the account.
    /// </summary>
    AuthChangePassword,

    /// <summary>
    /// The projection phase is starting.
    /// </summary>
    AuthProject,

    /// <summary>
    /// The link speed calculation phase is starting.
    /// </summary>
    AuthLinkSpeed,

    /// <summary>
    /// The authentication request is being acknowledged.
    /// </summary>
    AuthAck,

    /// <summary>
    /// The authentication (after callback) phase is starting.
    /// </summary>
    ReAuthenticate,

    /// <summary>
    /// The client has successfully completed authentication.
    /// </summary>
    Authenticated,

    /// <summary>
    /// The line is about to disconnect in preparation for callback.
    /// </summary>
    PrepareForCallback,

    /// <summary>
    /// The client is delaying in order to give the modem time to reset itself in preparation for callback.
    /// </summary>
    WaitForModemReset,

    /// <summary>
    /// The client is waiting for an incoming call from the remote access server.
    /// </summary>
    WaitForCallback,

    /// <summary>
    /// The projection result information has been made available.
    /// </summary>
    Projected,

    /// <summary>
    /// The user authentication is being started or reattempted.
    /// </summary>
    StartAuthentication,

    /// <summary>
    /// The client has been called back and is about to resume authentication.
    /// </summary>
    CallbackComplete,

    /// <summary>
    /// The client is logging on to the network.
    /// </summary>
    LogOnNetwork,

    /// <summary>
    /// The subentry within a multi-link connection has been connected.
    /// </summary>
    SubEntryConnected,

    /// <summary>
    /// The subentry within a multi-link connection has been disconnected.
    /// </summary>
    SubEntryDisconnected,

    /// <summary>
    /// The client is applying settings.
    /// </summary>
    ApplySettings,

    /// <summary>
    /// The client has entered an interactive state.
    /// </summary>
    Interactive = Ras.RASCS_PAUSED,

    /// <summary>
    /// The client is starting to retry authentication.
    /// </summary>
    RetryAuthentication,

    /// <summary>
    /// The client has entered the callback state.
    /// </summary>
    CallbackSetByCaller,

    /// <summary>
    /// The client password has expired.
    /// </summary>
    PasswordExpired,

    /// <summary>
    /// The client has been paused to display a custom authentication user interface.
    /// </summary>
    InvokeEapUI,

    /// <summary>
    /// The client has connected successfully.
    /// </summary>
    Connected = Ras.RASCS_DONE,

    /// <summary>
    /// The client has disconnected or failed a connection attempt.
    /// </summary>
    Disconnected,
}

/// <summary>
/// Defines the states for Internet Key Exchange version 2 (IKEv2) virtual private network (VPN) tunnel connections.
/// </summary>
/// <remarks>These states are not available to other tunneling protocols.</remarks>
public enum RasConnectionSubState
{
    /// <summary>
    /// The connection state does not have a sub-state.
    /// </summary>
    None = 0,

    /// <summary>
    /// The underlying internet interface of the connection is down and the connection is waiting for an internet interface to come online.
    /// </summary>
    Dormant,

    /// <summary>
    /// The internet interface has come online and the connection is switching over to this new interface through Mobile IKE (MOBIKE) update.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// The Mobile IKE (MOBIKE) update has completed and the connection has switched over successfully to the new internet interface.
    /// </summary>
    Reconnected = Ras.RASCSS_DONE
}