using NetSeal.Core.Models;
using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.Rras;

namespace NetSeal.Core.Services.Ras;

public partial class RasDialer : IRasDialer
{
    private IntPtr? currentConnectionHandle;

    /// <summary>
    /// 使用指定的电话簿条目进行拨号。
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿 (PBK) 文件的完整路径和文件名。
    /// 如果此参数为 null，则使用当前用户默认的电话簿文件。
    /// </param>
    /// <param name="entryName"></param>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    private static unsafe void Connect(string? phoneBook, string entryName, string userName, string password, out IntPtr handle)
    {
        var p = new RASDIALPARAMSW
        {
            dwSize = (uint)sizeof(RASDIALPARAMSW),
            szEntryName = entryName,
            szPhoneNumber = "",
            szCallbackNumber = "",
            szUserName = userName,
            szPassword = password,
            szDomain = "",
        };

        uint ret = PInvoke.RasDial(null, phoneBook, in p, 0, null, out var conn);
        handle = conn;
        if (ret != (uint)WIN32_ERROR.ERROR_SUCCESS)
        {
            uint hangupRet = PInvoke.RasHangUp(conn); // 程序最终应当断开连接，即使连接并未成功。
            if (hangupRet != (uint)WIN32_ERROR.ERROR_SUCCESS)
            {
                var exceptions = new List<Exception>
                {
                    new Win32Exception((int)ret, $"拨号连接失败：{GetErrorString(ret)}"),
                    new Win32Exception((int)hangupRet, $"断开拨号连接失败：{GetErrorString(ret)}")
                };

                throw new AggregateException(exceptions);
            }

            throw new Win32Exception((int)ret, $"拨号连接失败: {GetErrorString(ret)}");
        }
    }

    /// <summary>
    /// 断开指定 PPPoE 连接。
    /// </summary>
    /// <param name="entryName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="Win32Exception"></exception>
    private static void Disconnect(string? entryName = null)
    {
        var connection = GetActivePppoeConnection() ?? throw new InvalidOperationException("当前没有活跃的连接项目。");

        if (entryName != null)
            ArgumentOutOfRangeException.ThrowIfNotEqual(entryName, connection.szEntryName.ToString());

        uint ret = PInvoke.RasHangUp(connection!.hrasconn);

        if (ret != (uint)WIN32_ERROR.ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"断开拨号连接失败: {GetErrorString(ret)}");
    }

    /// <summary>
    /// 获取当前已接通的 Pppoe 连接。若没有已接通的 Pppoe 连接，则返回 null。
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    private static RASCONNW? GetActivePppoeConnection()
    {
        var connections = ListActiveRasConnections();
        foreach (var connection in connections)
        {
            if (string.Equals(connection.szDeviceType.ToString(), "PPPoE", StringComparison.OrdinalIgnoreCase))
                return connection;
        }

        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    private unsafe static List<RASCONNW> ListActiveRasConnections()
    {
        uint cb = 0;
        uint ret = PInvoke.RasEnumConnections(null, ref cb, out uint count);

        if (ret == (uint)WIN32_ERROR.ERROR_SUCCESS && count == 0)
            return [];

        if (ret != PInvoke.ERROR_BUFFER_TOO_SMALL)
            throw new Win32Exception((int)ret, $"获取活跃连接失败：{GetErrorString(ret)}");

        int size = sizeof(RASCONNW);
        int rasCount = (int)(cb / size);
        var buffer = new RASCONNW[rasCount];
        buffer[0].dwSize = (uint)size;

        ret = PInvoke.RasEnumConnections(buffer, ref cb, out count);

        if (ret != (uint)WIN32_ERROR.ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"获取活跃连接失败：{GetErrorString(ret)}");

        List<RASCONNW> result = [];
        for (int i = 0; i < count; i++)
            result.Add(buffer[i]);

        return result;
    }

    /// <summary>
    /// 列出指定电话簿里的所有连接名。
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿 (PBK) 文件的完整路径和文件名。
    /// 如果此参数为 null，则使用当前用户默认的电话簿文件。
    /// </param>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    private unsafe static List<RASENTRYNAMEW> ListAllEntries(string? phoneBook)
    {
        uint cb = 0;
        uint ret = PInvoke.RasEnumEntries(null, phoneBook, ref cb, out uint count);

        if (ret == (uint)WIN32_ERROR.ERROR_SUCCESS && count == 0)
            return [];

        if (ret != PInvoke.ERROR_BUFFER_TOO_SMALL)
            throw new Win32Exception((int)ret, $"获取电话簿条目失败：{GetErrorString(ret)}");

        int size = sizeof(RASENTRYNAMEW);
        int entriesCount = (int)(cb / size);
        RASENTRYNAMEW[] buffer = new RASENTRYNAMEW[entriesCount];
        buffer[0].dwSize = (uint)size;

        ret = PInvoke.RasEnumEntries(null, phoneBook, ref buffer[0], ref cb, out count);

        if (ret != (uint)WIN32_ERROR.ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"获取电话簿条目失败：{GetErrorString(ret)}");

        List<RASENTRYNAMEW> result = [];
        for (int i = 0; i < count; i++)
            result.Add(buffer[i]);

        return result;
    }

    public static unsafe void RasSetCredentials(string? phoneBook, string entryName, string username, string password)
    {
        var cred = new RASCREDENTIALSW
        {
            dwSize = (uint)sizeof(RASCREDENTIALSW),
            dwMask = PInvoke.RASCM_UserName | PInvoke.RASCM_Password,
            szUserName = username,
            szPassword = password,
            szDomain = "",
        };

        var ret = PInvoke.RasSetCredentials(phoneBook, entryName, in cred, false);
        if (ret != (uint)WIN32_ERROR.ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"设置凭据失败。错误信息：{GetErrorString(ret)}");
    }

    /// <summary>
    /// 创建 Entry 条目。
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿 (PBK) 文件的完整路径和文件名。
    /// 如果此参数为 null，则使用当前用户默认的电话簿文件。
    /// </param>
    /// <param name="entryName"></param>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    /// <exception cref="Win32Exception"></exception>
    private static unsafe void CreateEntry(string? phoneBook, string entryName, string? userName, string? password)
    {
        uint ret = PInvoke.RasValidateEntryName(phoneBook, entryName);

        if (ret == (uint)WIN32_ERROR.ERROR_INVALID_NAME)
            throw new ArgumentException("名称包含非法字符。", nameof(entryName));

        if (ret != (uint)WIN32_ERROR.ERROR_SUCCESS && ret != (uint)WIN32_ERROR.ERROR_ALREADY_EXISTS)
            throw new Win32Exception((int)ret, $"验证输入名称失败：{GetErrorString(ret)}");

        var entry = new RASENTRYW
        {
            dwSize = (uint)sizeof(RASENTRYW),
            dwfOptions = PInvoke.RASEO_RemoteDefaultGateway
                | PInvoke.RASEO_ShowDialingProgress
                | PInvoke.RASEO_PreviewDomain,
            dwType = PInvoke.RASET_Broadband,
            dwfNetProtocols = PInvoke.RASNP_Ip | PInvoke.RASNP_Ipv6,
            dwFramingProtocol = PInvoke.RASFP_Ppp,
            szDeviceType = PInvoke.RASDT_PPPoE,
            szDeviceName = "WAN Miniport (PPPOE)",
            szLocalPhoneNumber = "",
            guidId = Guid.NewGuid(),
            dwEncryptionType = PInvoke.ET_Optional,
        };

        ret = PInvoke.RasSetEntryProperties(phoneBook, entryName, in entry, entry.dwSize, null, 0);

        if (ret != 0)
            throw new Win32Exception((int)ret, $"创建电话簿条目失败。错误信息：{GetErrorString(ret)}");

        if (!string.IsNullOrEmpty(userName))
        {
            var cred = new RASCREDENTIALSW
            {
                dwSize = (uint)sizeof(RASCREDENTIALSW),
                dwMask = PInvoke.RASCM_UserName | PInvoke.RASCM_Password,
                szDomain = "",
                szUserName = userName,
                szPassword = password
            };

            ret = PInvoke.RasSetCredentials(phoneBook, entryName, in cred, false);
            if (ret != 0)
                throw new Win32Exception((int)ret, $"设置凭据失败。错误信息：{GetErrorString(ret)}");
        }
    }

    /// <summary>
    /// 删除指定 Entry 条目。
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿 (PBK) 文件的完整路径和文件名。
    /// 如果此参数为 null，则使用当前用户默认的电话簿文件。
    /// </param>
    /// <param name="entryName"></param>
    /// <exception cref="Win32Exception"></exception>
    private static void RemoveEntry(string? phoneBook, string entryName)
    {
        uint ret = PInvoke.RasDeleteEntry(phoneBook, entryName);
        if (ret != (uint)WIN32_ERROR.ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"删除 Entry 条目失败。错误信息：{GetErrorString(ret)}");
    }

    /// <summary>
    /// 判断条目是否已存在
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿 (PBK) 文件的完整路径和文件名。
    /// 如果此参数为 null，则使用当前用户默认的电话簿文件。
    /// </param>
    /// <param name="entryName"></param>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    private static bool IsEntryExists(string? phoneBook, string entryName)
    {
        uint ret = PInvoke.RasValidateEntryName(phoneBook, entryName);
        if (ret == (uint)WIN32_ERROR.ERROR_SUCCESS)
            return false;

        if (ret == (uint)WIN32_ERROR.ERROR_ALREADY_EXISTS)
            return true;

        if (ret == PInvoke.ERROR_CANNOT_FIND_PHONEBOOK_ENTRY)
            return false;

        throw new Win32Exception((int)ret, GetErrorString(ret));
    }

    /// <summary>
    /// 错误码转文本。
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    private unsafe static string GetErrorString(uint code)
    {
        char* buffer = stackalloc char[512];
        for (int i = 0; i < 512; i++)
            buffer[i] = '\0';
        WIN32_ERROR result = (WIN32_ERROR)PInvoke.RasGetErrorString(code, buffer, 512);

        if (result == WIN32_ERROR.ERROR_SUCCESS)
            return new string(buffer);

        return $"未知错误代码 (0x{code:X})";
    }

    public async Task RasCreateEntryAsync(string? phoneBook, string entryName, string? username, string? password)
    {
        if (!await Task.Run(() => IsEntryExists(phoneBook, entryName)))
        {
            await Task.Run(() => CreateEntry(phoneBook, entryName, username, password));
        }
    }

    public async Task RasRemoveEntryAsync(string? phoneBook, string entryName)
    {
        await Task.Run(() => RemoveEntry(phoneBook, entryName));
    }

    public async Task RasSetCredentialsAsync(string? phoneBook, string entryName, string username, string password)
    {
        await Task.Run(() => RasSetCredentials(phoneBook, entryName, username, password));
    }

    public async Task<RasConnection?> GetActiveRasConnectionAsync()
    {
        var connection = await Task.Run(() => GetActivePppoeConnection());
        if (connection is not RASCONNW conn)
            return null;

        return new RasConnection
        {
            PhoneBookName = conn.szPhonebook.ToString(),
            EntryName = conn.szEntryName.ToString(),
            Handle = conn.hrasconn,
        };
    }

    public async Task RasConnectAsync(string? phoneBook, string entryName, string username, string password, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            Connect(null, entryName, username, password, out IntPtr handle);
            currentConnectionHandle = handle;
        }, cancellationToken);
    }

    public async Task RasDisconnectAsync(string? entryName, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Disconnect(entryName), cancellationToken);
    }

    public async Task<bool> GetIsConnectionManagedByCurrentSessionAsync()
    {
        var connection = await GetActiveRasConnectionAsync()
            ?? throw new InvalidOperationException("当前不存在活跃状态的 Ras 连接项目。");

        return currentConnectionHandle == connection.Handle;
    }

    public async Task<bool> IsConnectionManagedByCurrentSession(RasConnection connection)
    {
        return currentConnectionHandle == connection.Handle;
    }
}