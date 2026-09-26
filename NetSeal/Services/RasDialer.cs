using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NetSeal.Services;
public static class RasDialer
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RASENTRYNAME
    {
        public int dwSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]  // 256 + 1
        public string szEntryName;

        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]  // MAX_PATH(260) + 1
        public string szPhonebookPath;
    }

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasEnumEntries(
    string? reserved,           // 必须 NULL
    string? lpszPhonebook,      // NULL = 默认电话簿
    [In, Out] RASENTRYNAME[] lprasentryname,  // 输出数组
    ref uint lpcb,              // 缓冲区字节数
    ref uint lpcEntries);       // 输出：实际条目数

    private const uint ERROR_BUFFER_TOO_SMALL = 122;
    private const uint ERROR_SUCCESS = 0;

    // ---------- P/Invoke ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RASDIALPARAMS
    {
        public int dwSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string szEntryName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] public string szPhoneNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] public string szCallbackNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string szUserName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string szPassword;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string szDomain;
    }

    private delegate void RasDialFunc(uint unMsg, int rascs, uint dwError, uint dwExtendedError);

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasDial(
        IntPtr lpRasDialExtensions,
        string? lpszPhonebook,
        ref RASDIALPARAMS lprasdialparams,
        uint dwNotifierType,
        IntPtr lpvNotifier,
        out IntPtr lphRasConn);

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasHangUp(IntPtr hRasConn);

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasGetErrorString(
        uint uErrorValue,
        [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder lpszErrorString,
        uint cBufSize);

    /// <summary>
    /// 拨号。返回连接句柄（用于断开），失败抛异常。
    /// </summary>
    /// <param name="entryName"></param>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IntPtr Dial(string userName, string password)
    {
        var p = new RASDIALPARAMS
        {
            dwSize = Marshal.SizeOf<RASDIALPARAMS>(),
            szEntryName = "netseal_dialer",
            szPhoneNumber = "",
            szCallbackNumber = "",
            szUserName = userName,
            szPassword = password,
            szDomain = "",
        };

        uint ret = RasDial(IntPtr.Zero, null, ref p, 0, IntPtr.Zero, out var conn);
        if (ret != 0)
            throw new InvalidOperationException($"RasDial 失败 ({ret}): {GetErrorString(ret)}");

        return conn;
    }

    /// <summary>
    /// 断开连接。
    /// </summary>
    /// <param name="conn"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void HangUp(IntPtr conn)
    {
        if (conn != IntPtr.Zero)
        {
            uint ret = RasHangUp(conn);
            if (ret != 0)
                throw new InvalidOperationException($"RasDial 失败 ({ret}): {GetErrorString(ret)}");
        }
    }

    /// <summary>错误码转文本。</summary>
    public static string GetErrorString(uint code)
    {
        var stringBuilder = new System.Text.StringBuilder(512);
        uint ret = RasGetErrorString(code, stringBuilder, 512);

        if (ret != 0)
            return $"未知 RAS 错误 (0x{code:X8})";

        return stringBuilder.ToString();
    }

    /// <summary>列出系统电话簿里的所有连接名。</summary>
    public static string[] ListEntries()
    {
        const int MaxEntries = 32;

        var entries = new RASENTRYNAME[MaxEntries];
        for (int i = 0; i < MaxEntries; i++)
            entries[i].dwSize = Marshal.SizeOf<RASENTRYNAME>();

        uint cb = (uint)(Marshal.SizeOf<RASENTRYNAME>() * MaxEntries);
        uint count = 0;

        uint ret = RasEnumEntries(null, null, entries, ref cb, ref count);

        if (ret == ERROR_BUFFER_TOO_SMALL)
            throw new InvalidOperationException($"连接数超过 {MaxEntries}，需要 {(int)cb} 字节缓冲区");

        if (ret != ERROR_SUCCESS)
            throw new InvalidOperationException($"RasEnumEntries 失败 ({ret}): {GetErrorString(ret)}");

        var result = new string[count];
        for (int i = 0; i < count; i++)
            result[i] = entries[i].szEntryName;

        return result;
    }

    /// <summary>检查是否已拨上（任意 PPP 接口 Up）。</summary>
    public static bool IsConnected()
    {
        return System.Net.NetworkInformation.NetworkInterface
            .GetAllNetworkInterfaces()
            .Any(n => n.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ppp
                   && n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up);
    }
}