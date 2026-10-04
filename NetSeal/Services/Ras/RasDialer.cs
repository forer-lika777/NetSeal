using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static NetSeal.Services.Ras.NativeMethods;

namespace NetSeal.Services.Ras;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:使用 “LibraryImportAttribute” 而不是 “DllImportAttribute” 在编译时生成 P/Invoke 封送代码", Justification = "<挂起>")]
public static class RasDialer
{
    #region P/Invoke

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rasvalidateentrynamew">RasValidateEntryName</see></b> 函数验证连接项名称的格式。该名称必须至少包含一个非空白字母数字字符。
    /// </summary>
    /// <param name="lpszPhonebook">
    /// <para>指向以 null 结尾的字符串的指针，该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 如果此参数为 <b>NULL</b> 则该函数使用当前的默认电话簿文件。</para>
    /// </param>
    /// <param name="lpszEntry">
    /// <para>指向指定条目名称的以 null 结尾的字符串的指针。</para>
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值是以下错误代码之一或来自 
    /// <see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see> 
    /// 或 Winerror.h 的值：</para>
    /// <list type="bullet">
    /// <item>
    /// <term>ERROR_ALREADY_EXISTS</term>
    /// <description>指定的电话簿中已存在条目名称。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_CANNOT_FIND_PHONEBOOK</term>
    /// <description>指定的电话簿不存在。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_INVALID_NAME</term>
    /// <description>指定条目名称的格式无效。</description>
    /// </item>
    /// </list>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasValidateEntryName(
        string? lpszPhonebook, 
        string lpszEntry);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rassetentrypropertiesw">RasSetEntryProperties</see></b> 函数更改电话簿中条目的连接信息，或创建新的电话簿条目。
    /// </summary>
    /// <param name="lpszPhonebook">
    /// <para>指向以 null 结尾的字符串的指针，该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 如果此参数为 <b>NULL</b>，则该函数使用当前的默认电话簿文件。
    /// 默认电话簿文件是用户在 <b>用户首选项</b>、<b>拨号网络</b> 对话框中选择的文件。</para>
    /// </param>
    /// <param name="lpszEntry">
    /// <para>指向指定条目名称的以 null 结尾的字符串的指针。</para>
    /// <para>如果条目名称与现有条目匹配，<b>RasSetEntryProperties</b> 修改该条目的属性。</para>
    /// <para>如果条目名称与现有条目不匹配，<b>RasSetEntryProperties</b> 创建新的电话簿条目。
    /// 对于新条目，请在调用 <b>RasSetEntryProperties</b> 之前调用 <see cref="RasValidateEntryName(string?, string)"/> 函数来验证条目名称。</para>
    /// </param>
    /// <param name="lpRasEntry">
    /// <para>指向 <see cref="RASENTRY"/> 结构的指针，该结构指定要与 *lpszEntry* 参数指示的电话簿条目关联的新连接数据。</para>
    /// 调用方必须为 <see cref="RASENTRY"/> 结构中的以下成员提供值：
    /// <list type="bullet">
    /// <item><see cref="RASENTRY.size"/></item>
    /// <item><see cref="RASENTRY.phoneNumber"/></item>
    /// <item><see cref="RASENTRY.deviceName"/></item>
    /// <item><see cref="RASENTRY.deviceType"/></item>
    /// <item><see cref="RASENTRY.framingProtocol"/></item>
    /// <item><see cref="RASENTRY.options"/></item>
    /// <item><see cref="RASENTRY.entryType"/></item>
    /// </list>
    /// <para>如果未为这些成员提供值，<b>RasSetEntryProperties</b> 失败并返回 <b>ERROR_INVALID_PARAMATER</b></para>
    /// <para>该结构可能后跟以 null 结尾的备用电话号码字符串数组。最后一个字符串由两个连续 null 字符终止。dwAlternateOffset <see cref="RASENTRY"/> 结构的成员包含第一个字符串的偏移量。</para>
    /// </param>
    /// <param name="dwEntryInfoSize">
    /// 指定由 <paramref name="lpRasEntry"/> 参数标识的缓冲区的大小（以字节为单位）。
    /// </param>
    /// <param name="lpbDeviceInfo">
    /// 指向指定特定于设备的配置信息的缓冲区的指针。这是不透明的 TAPI 设备配置信息。
    /// 有关 TAPI 设备配置的详细信息，请参阅平台 SDK 中 
    /// <a href="https://learn.microsoft.com/en-us/windows/desktop/Tapi/telephony-application-programming-interfaces">电话应用程序编程接口（TAPI）</a> 中的
    /// <a href="https://learn.microsoft.com/en-us/windows/desktop/api/tapi/nf-tapi-linegetdevconfig">lineGetDevConfig</a> 函数。
    /// </param>
    /// <param name="dwDeviceInfoSize">
    /// 指定 <paramref name="lpbDeviceInfo"/> 缓冲区的大小（以字节为单位）。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值为以下错误代码之一或来自
    /// <a href="https://learn.microsoft.com/en-us/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</a>
    /// 或 WinError.h 的值。
    /// <list type="table">
    /// <item>
    /// <term>ERROR_ACCESS_DENIED</term>
    /// <description>用户没有正确的权限。只有管理员才能完成此任务。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_BUFFER_INVALID</term>
    /// <description><paramref name="lpRasEntry"/> 指定的地址或缓冲区无效。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_CANNOT_OPEN_PHONEBOOK</term>
    /// <description>电话簿已损坏或缺少组件。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_INVALID_PARAMETER</term>
    /// <description><see cref="RASENTRY"/> 结构指向的 <paramref name="lpRasEntry"/> 参数不包含足够的信息，或者电话簿中不存在指定的条目。
    /// 请参阅 <paramref name="lpRasEntry"/> 的说明，了解所需的信息。</description>
    /// </item>
    /// </list></para>
    /// </returns>

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasSetEntryProperties(
        string? lpszPhonebook,
        string lpszEntry,
        in RASENTRY lpRasEntry,
        uint dwEntryInfoSize, 
        [In] byte[]? lpbDeviceInfo,
        uint dwDeviceInfoSize);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rasgetentrypropertiesw">RasGetEntryProperties</see></b> 函数检索电话簿条目的属性。
    /// </summary>
    /// <param name="lpszPhonebook">
    /// <para>指向 null 终止字符串的指针，该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 如果此参数为 <b>NULL</b>，则该函数使用当前的默认电话簿文件。
    /// 默认电话簿文件是用户在 <b>用户首选项拨号网络</b> 对话框中选择的文件。</para>
    /// </param>
    /// <param name="lpszEntry">
    /// <para>指向指定现有条目名称的 null 终止字符串的指针。
    /// 如果指定了空字符串，该函数将返回由 <paramref name="lpRasEntry"/> 指向的缓冲区中的默认值，并 <paramref name="lpbDeviceInfo"/> 参数。</para>
    /// </param>
    /// <param name="lpRasEntry">
    /// <para>指向 <see cref="RASENTRY"/> 结构的指针，后跟备用电话号码列表的其他字节（如果有）。</para>
    /// <para>输出时，结构接收与 <paramref name="lpRasEntry"/> 参数指定的电话簿条目关联的连接数据。</para>
    /// <para>在输入时，将结构的 dwSize 成员设置为 sizeof(<see cref="RASENTRY"/>)，以标识结构的版本。</para>
    /// 此参数可以 <b>NULL</b>。</param>
    /// <param name="lpdwEntryInfoSize">
    /// <para>指向在输入上指定 <paramref name="lpRasEntry"/> 缓冲区的大小（以字节为单位）的变量的指针。</para>
    /// <para>在输出中，此变量接收所需的字节数。</para>
    /// <para>如果 <paramref name="lpRasEntry"/> 参数 <b>NULL</b>，则可以 <b>NULL</b> 此参数。</para>
    /// <para>若要确定所需的缓冲区大小，请调用 <b>RasGetEntryProperties</b>，并将 <paramref name="lpRasEntry"/> 设置为 NULL，<paramref name="lpdwEntryInfoSize"/> 设置为零。
    /// 该函数返回 <paramref name="lpdwEntryInfoSize"/> 中所需的缓冲区大小。</para>
    /// </param>
    /// <param name="lpbDeviceInfo">
    /// <para>不再使用此参数。调用函数应将此参数设置为 <b>NULL</b>。</para>
    /// </param>
    /// <param name="lpdwDeviceInfoSize">
    /// <para>此参数未使用。调用函数应将此参数设置为 <b>NULL</b>。</para>
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值是以下错误代码之一或来自 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see> 或 Winerror.h 的值。</para>
    /// <list type="table">
    /// <item>
    /// <term>ERROR_INVALID_PARAMETER</term>
    /// <description>该函数是使用无效参数调用的。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_INVALID_SIZE</term>
    /// <description><paramref name="lpRasEntry"/> 的 dwSize 成员的值太小。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_BUFFER_INVALID</term>
    /// <description><paramref name="lpRasEntry"/> 指定的地址或缓冲区无效。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_BUFFER_TOO_SMALL</term>
    /// <description><paramref name="lpdwEntryInfoSize"/> 中指示的缓冲区大小太小。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_CANNOT_FIND_PHONEBOOK_ENTRY</term>
    /// <description>电话簿条目不存在，或者电话簿文件已损坏且/或缺少组件。</description>
    /// </item>
    /// </list>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasGetEntryProperties(
        string? lpszPhonebook, 
        string lpszEntry, 
        ref RASENTRY? lpRasEntry, 
        ref uint? lpdwEntryInfoSize, 
        [Out] byte[]? lpbDeviceInfo, 
        ref uint? lpdwDeviceInfoSize);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rasdeleteentryw">RasDeleteEntry</see></b> 函数从电话簿中删除条目。
    /// </summary>
    /// <param name="lpszPhonebook">
    /// <para>指向以 null 结尾的字符串的指针，该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 如果此参数为 <b>NULL</b>，则该函数使用当前的默认电话簿文件。
    /// 默认电话簿文件是用户在 <b>用户首选项</b>、<b>拨号网络</b> 对话框中选择的文件。</para>
    /// </param>
    /// <param name="lpszEntry">
    /// 指向以 null 结尾的字符串的指针，该字符串指定要删除的现有条目的名称。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值是以下错误代码之一或来自 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see> 或 Winerror.h 的值。</para>
    /// <list type="table">
    /// <item>
    /// <term>ERROR_ACCESS_DENIED</term>
    /// <description>用户没有正确的权限。 只有管理员才能完成此任务。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_INVALID_NAME</term>
    /// <description><paramref name="lpszEntry"/> 中指定的条目名称不存在。</description>
    /// </item>
    /// </list>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasDeleteEntry(
        string? lpszPhonebook, 
        string lpszEntry);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rassetcredentialsw">RasSetCredentials</see></b> 函数设置与指定的 RAS 电话簿条目关联的用户凭据。
    /// </summary>
    /// <param name="lpszPhonebook">
    /// <para>指向以 null 结尾的字符串的指针，该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 如果此参数为 <b>NULL</b>，则该函数使用当前的默认电话簿文件。
    /// 默认电话簿文件是用户在 <b>用户首选项</b>、<b>拨号网络</b> 对话框中选择的文件。</para>
    /// </param>
    /// <param name="lpszEntry">
    /// 指向以 null 结尾的字符串的指针，该字符串指定电话簿条目的名称。
    /// </param>
    /// <param name="lpCredentials">
    /// 指向 <see cref="RASCREDENTIALS"/> 结构的指针，该结构指定要为指定的电话簿条目设置的用户凭据。
    /// 在调用 <b>RasSetCredentials</b> 之前，请将 <see cref="RASCREDENTIALS.size"/> 成员设置为 <c>sizeof(RASCREDENTIALS)</c>，并将 <see cref="RASCREDENTIALS.options"/> 成员设置为指示要设置的凭据信息。
    /// </param>
    /// <param name="fClearCredentials">
    /// 一个值，该值指定是否 <b>RasSetCredentials</b> 通过将现有凭据设置为空字符串“”来清除现有凭据。
    /// 如果此值为 <b>TRUE</b>，则 <paramref name="lpCredentials"/> 的 <see cref="RASCREDENTIALS.options"/> 成员为函数指示要设置为空字符串的目标凭据。
    /// 如果此值为 <b>FALSE</b>，则函数会依据相应 <see cref="RASCREDENTIALS"/> 成员的内容设置指示的凭据。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值是以下错误代码之一或来自 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see> 或 WinError.h 的值。</para>
    /// <list type="table">
    /// <item>
    /// <term>ERROR_CANNOT_OPEN_PHONEBOOK</term>
    /// <description>找不到指定的电话簿。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_INVALID_PARAMETER</term>
    /// <description><paramref name="lpCredentials"/> 参数 <b>NULL</b>，或者电话簿中不存在指定的条目。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_ACCESS_DENIED</term>
    /// <description>
    /// 出现以下情况之一：
    /// <list type="bullet">
    /// <item>调用应用程序尝试为单个用户连接设置默认凭据。只能为所有用户连接设置默认凭据。</item>
    /// <item>在类似所有用户连接的情况下，用户没有为所有用户设置预共享密钥或凭据的正确权限。只有管理员才能执行这样的操作。</item>
    /// </list>
    /// </description>
    /// </item>
    /// </list>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasSetCredentials(
        string? lpszPhonebook, 
        string lpszEntry, 
        in RASCREDENTIALS lpCredentials,
        [MarshalAs(UnmanagedType.Bool)] bool fClearCredentials);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rasenumentriesw">RasEnumEntries</see></b> 函数列出了远程访问电话簿中的所有条目名称。
    /// </summary>
    /// <param name="reserved">
    /// 保留；必须为 <b>NULL</b>。
    /// </param>
    /// <param name="lpszPhonebook">
    /// <para>指向字符串的变量，该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 如果此参数为 <b>NULL</b>，则该函数使用当前的默认电话簿文件。
    /// 默认电话簿文件是用户在 <b>用户首选项</b>、<b>拨号网络</b> 对话框中选择的文件。</para>
    /// <para>如果此参数为 <b>NULL</b>，则会从 AllUsers 配置文件和用户配置文件中的所有远程访问电话簿文件枚举这些条目。</para>
    /// </param>
    /// <param name="lprasentryname">
    /// <para>指向缓冲区的变量。在输出时，该缓冲区中接收一个 <see cref="RASENTRYNAME"/> 结构的数组，每个结构体对应一个电话簿条目。</para>
    /// <para>在输入时，应用程序必须将缓冲区中第一个 <see cref="RASENTRYNAME"/> 结构的 <see cref="RASENTRYNAME.size"/> 成员设置为 <c>sizeof(RASENTRYNAME)</c>，以标识要传递的结构的版本。</para>
    /// </param>
    /// <param name="lpcb">
    /// <para>指向一个变量，在输入时，该变量包含由 <paramref name="lprasentryname"/> 确定的缓冲区大小（以字节为单位）。</para>
    /// <para>指向一个变量，在输出时，该变量包含所有电话簿条目所需的 <see cref="RASENTRYNAME"/> 结构的数组的大小（以字节为单位）。</para>
    /// <para>若要确定所需的缓冲区大小，请调用 RasEnumEntries，将 <paramref name="lprasentryname"/> 设置为 NULL，<paramref name="lpcb"/> 设置为零。
    /// 该函数将在 <paramref name="lpcb"/> 中返回所需的缓冲区大小，并返回 <b>ERROR_BUFFER_TOO_SMALL</b> 的错误代码。</para>
    /// </param>
    /// <param name="lpcEntries">
    /// 指向一个变量，该变量接收写入到 <paramref name="lprasentryname"/> 指定的缓冲区的电话簿条目数。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值是以下错误代码之一或来自 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see> 或 WinError.h 的值。</para>
    /// <list type="table">
    /// <item>
    /// <term>ERROR_BUFFER_TOO_SMALL</term>
    /// <description><para><paramref name="lprasentryname"/> 缓冲区不够大。
    /// <paramref name="lpcb"/> 参数小于 <paramref name="lprasentryname"/> 参数中的 <see cref="RASENTRYNAME.size"/> 成员，该参数应在调用函数之前设置。
    /// 该函数将在 <paramref name="lpcb"/> 中返回所需的缓冲区大小。</para> 
    /// <para>Windows Vista 或更新版本：<paramref name="lprasentryname"/> 缓冲区可能设置为 <b>NULL</b>，<paramref name="lpcb"/> 可能设置为零。 该函数将在 <paramref name="lpcb"/> 中返回所需的缓冲区大小。</para></description>
    /// </item>
    /// <item>
    /// <term>ERROR_INVALID_SIZE</term>
    /// <description>由 <paramref name="lprasentryname"/> 中 <see cref="RASENTRYNAME.size"/> 的值指向的结构版本在当前平台上不受支持。
    /// 例如，在 Windows 95 上，如果 <see cref="RASENTRYNAME.size"/> 指示的 <see cref="RASENTRYNAME"/> 包含 <see cref="RASENTRYNAME.phoneBookType"/> 和 <see cref="RASENTRYNAME.phoneBookPath"/> 成员，<b>RasEnumEntries</b> 将返回此错误，
    /// 因为这些成员在 Windows 95 上不受支持（它们仅在 Windows 2000 及更高版本上受支持）。</description>
    /// </item>
    /// <item>
    /// <term>ERROR_NOT_ENOUGH_MEMORY</term>
    /// <description>该函数无法分配足够的内存来完成此操作。</description>
    /// </item>
    /// </list>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasEnumEntries(
        string? reserved, 
        string? lpszPhonebook, 
        [In, Out] RASENTRYNAME[]? lprasentryname,
        ref uint lpcb,
        out uint lpcEntries);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rasenumconnectionsa">RasEnumConnections</see></b> 函数列出了所有活动的 RAS 连接。它返回每个连接的句柄和电话簿条目名称。
    /// </summary>
    /// <param name="lprasconn">
    /// <para>指向在输出上接收 <see cref="RASCONN"/> 结构的缓冲区的指针，每个 RAS 连接都有一个。</para>
    /// <para>在输入时，应用程序必须将缓冲区中第一个 <see cref="RASCONN"/> 结构的 dwSize 成员设置为 sizeof（RASCONN），以便标识要传递的结构的版本。</para>
    /// </param>
    /// <param name="lpcb">
    /// <para>在输入时，该变量包含由 <paramref name="lprasconn"/> 指定的缓冲区的大小（以字节为单位）。</para>
    /// <para>在输出时，该函数将此变量设置为枚举 RAS 连接所需的字节数。</para>
    /// <para>若要确定所需的缓冲区大小，请调用 <b>RasEnumConnections</b>，并将 <paramref name="lprasconn"/> 设置为 <b>NULL</b>。
    /// <paramref name="lpcb"/> 应设置为零。 该函数在 <paramref name="lpcb"/> 中返回所需的缓冲区大小，并返回 <b>ERROR_BUFFER_TOO_SMALL</b> 的错误代码。</para>
    /// </param>
    /// <param name="lpcConnections">
    /// 指向一个变量的指针，该变量接收写入 <paramref name="lprasconn"/> 指定的缓冲区 <see cref="RASCONN"/> 结构的数目。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值是以下错误代码之一或来自 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see> 或 WinError.h 的值。</para>
    /// <list type="table">
    /// <item>
    /// <term>ERROR_BUFFER_TOO_SMALL</term>
    /// <description><paramref name="lprasconn"/> 缓冲区不够大。
    /// <paramref name="lpcb"/> 参数小于 <paramref name="lprasconn"/> 参数中的 <see cref="RASCONN.size"/> 成员，该参数应在调用函数之前设置。
    /// 该函数在 <paramref name="lpcb"/> 中返回所需的缓冲区大小。</description>
    /// </item>
    /// </list>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasEnumConnections(
        [In, Out] RASCONN[]? lprasconn,
        ref uint lpcb,
        out uint lpcConnections);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rasdialw">RasDial</see></b> 
    /// 函数在 RAS 客户端和 RAS 服务器之间建立 RAS 连接。
    /// 连接数据包括回调和用户身份验证信息。
    /// </summary>
    /// <param name="lpRasDialExtensions">
    /// 指向 <see cref="RASDIALEXTENSIONS"/> 结构的指针，该结构指定要启用的一组 RasDial 扩展功能。
    /// 如果不需要启用这些功能，请将此参数设置为 NULL 。
    /// </param>
    /// <param name="lpszPhonebook">
    /// 指向以 null 结尾的字符串的指针，该字符串指定电话簿 (PBK) 文件的完整路径和文件名。
    /// 如果此参数为 NULL，则该函数使用当前默认的电话簿文件。
    /// 默认电话簿文件是用户在“<b>拨号网络</b>”对话框的“<b>用户首选项</b>”属性表中选择的文件。
    /// </param>
    /// <param name="lprasdialparams">
    /// <para>指向 <see cref="RASDIALPARAMS"/> 结构的指针，该结构指定 RAS 连接的调用参数。
    /// 使用 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/api/ras/nf-ras-rasgetentrydialparamsa">RasGetEntryDialParams</see> 函数检索特定电话簿条目的此结构的副本。</para>
    /// <para>调用方必须将 <see cref="RASDIALPARAMS"/> 结构的 dwSize 成员设置为 <c>sizeof(RASDIALPARAMS)</c> ，以标识所传递的结构的版本。</para>
    /// <para>如果 <see cref="RASDIALPARAMS"/> 结构的 szPhoneNumber 成员为空字符串，则 RasDial 将使用存储在电话簿条目中的电话号码。</para>
    /// </param>
    /// <param name="dwNotifierType">
    /// 指定 <paramref name="lpvNotifier"/> 参数的性质。
    /// 如果 <paramref name="lpvNotifier"/> 为 NULL，则忽略 <paramref name="dwNotifierType"/>。
    /// 如果 <paramref name="lpvNotifier"/> 不为 NULL，请将 <paramref name="dwNotifierType"/> 设置为以下值之一：
    /// <list type="table">
    /// <item>
    /// <term>0</term>
    /// <description><paramref name="lpvNotifier"/> 参数指向 <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc">RasDialFunc</see> 回调函数。</description>
    /// </item>
    /// <item>
    /// <term>1</term>
    /// <description><paramref name="lpvNotifier"/> 参数指向 <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc1">RasDialFunc1</see> 回调函数。</description>
    /// </item>
    /// <item>
    /// <term>2</term>
    /// <description><paramref name="lpvNotifier"/> 参数指向 <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc2">RasDialFunc2</see> 回调函数。</description>
    /// </item>
    /// </list>
    /// </param>
    /// <param name="lpvNotifier">
    /// <para>指定窗口句柄或 <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc">RasDialFunc</see>、
    /// <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc1">RasDialFunc1</see> 或 
    /// <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc2">RasDialFunc2</see>
    /// 回调函数以接收 RasDial 事件通知。
    /// <paramref name="dwNotifierType"/> 参数指定 <paramref name="lpvNotifier"/> 的性质。
    /// 有关更多详细信息，请参阅前面的说明。</para>
    /// <para>如果此参数不为 <b>NULL</b>，则 <b>RasDial</b> 会为每个 <b>RasDial</b> 事件向窗口发送消息或调用回调函数。
    /// 此外，<b>RasDial</b> 调用以异步方式运行：<b>RasDial</b> 在建立连接之前立即返回，并通过窗口或回调函数传达其进度。</para>
    /// <para>如果 <paramref name="lpvNotifier"/> 为 NULL，则 <b>RasDial</b> 调用将同步运行：<b>RasDial</b> 在连接尝试成功完成或失败之前不会返回。</para>
    /// <para>如果 <paramref name="lpvNotifier"/> 不为 NULL，则初始调用 <b>RasDial</b> 后，随时可能会向窗口或回调函数发出通知。
    /// 发生以下事件之一时，通知结束：
    /// <list type="bullet">
    /// <item>已建立连接。换句话说，RAS 连接状态 RASCS_Connected。</item>
    /// <item>连接失败。换句话说，<i>dwError</i> 是非零值。</item>
    /// <item>在连接上调用 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/api/ras/nf-ras-rashangupa">RasHangUp</see>。</item>
    /// </list>
    /// </para>
    /// </param>
    /// <param name="lphRasConn">
    /// 指向 <b>HRASCONN</b> 类型的变量的指针。
    /// 在调用 <b>RasDial</b> 之前，将 <b>HRASCONN</b> 变量设置为 <b>NULL</b>。
    /// 如果 <b>RasDial</b> 成功，它将 RAS 连接的句柄存储到 <paramref name="lphRasConn"/>。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b> ，并在 <paramref name="lphRasConn"/> 指向的变量中返回 RAS 连接的句柄。</para>
    /// <para>如果函数失败，则返回值为“<see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see>”或“Winerror.h”。</para>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasDial(
        in RASDIALEXTENSIONS lpRasDialExtensions,
        string? lpszPhonebook,
        in RASDIALPARAMS lprasdialparams,
        uint dwNotifierType,
        IntPtr? lpvNotifier,
        out IntPtr lphRasConn);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rasdialw">RasDial</see></b> 
    /// 函数在 RAS 客户端和 RAS 服务器之间建立 RAS 连接。
    /// 连接数据包括回调和用户身份验证信息。
    /// </summary>
    /// <param name="lpRasDialExtensions">
    /// 指向 <see cref="RASDIALEXTENSIONS"/> 结构的指针，该结构指定要启用的一组 RasDial 扩展功能。
    /// 如果不需要启用这些功能，请将此参数设置为 NULL 。
    /// </param>
    /// <param name="lpszPhonebook">
    /// 指向以 null 结尾的字符串的指针，该字符串指定电话簿 (PBK) 文件的完整路径和文件名。
    /// 如果此参数为 NULL，则该函数使用当前默认的电话簿文件。
    /// 默认电话簿文件是用户在“<b>拨号网络</b>”对话框的“<b>用户首选项</b>”属性表中选择的文件。
    /// </param>
    /// <param name="lprasdialparams">
    /// <para>指向 <see cref="RASDIALPARAMS"/> 结构的指针，该结构指定 RAS 连接的调用参数。
    /// 使用 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/api/ras/nf-ras-rasgetentrydialparamsa">RasGetEntryDialParams</see> 函数检索特定电话簿条目的此结构的副本。</para>
    /// <para>调用方必须将 <see cref="RASDIALPARAMS"/> 结构的 dwSize 成员设置为 <c>sizeof(RASDIALPARAMS)</c> ，以标识所传递结构的版本。</para>
    /// <para>如果 <see cref="RASDIALPARAMS"/> 结构的 szPhoneNumber 成员为空字符串，则 RasDial 将使用存储在电话簿条目中的电话号码。</para>
    /// </param>
    /// <param name="dwNotifierType">
    /// 指定 <paramref name="lpvNotifier"/> 参数的性质。
    /// 如果 <paramref name="lpvNotifier"/> 为 NULL，则忽略 <paramref name="dwNotifierType"/>。
    /// 如果 <paramref name="lpvNotifier"/> 不为 NULL，请将 <paramref name="dwNotifierType"/> 设置为以下值之一：
    /// <list type="table">
    /// <item>
    /// <term>0</term>
    /// <description><paramref name="lpvNotifier"/> 参数指向 <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc">RasDialFunc</see> 回调函数。</description>
    /// </item>
    /// <item>
    /// <term>1</term>
    /// <description><paramref name="lpvNotifier"/> 参数指向 <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc1">RasDialFunc1</see> 回调函数。</description>
    /// </item>
    /// <item>
    /// <term>2</term>
    /// <description><paramref name="lpvNotifier"/> 参数指向 <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc2">RasDialFunc2</see> 回调函数。</description>
    /// </item>
    /// </list>
    /// </param>
    /// <param name="lpvNotifier">
    /// <para>指定窗口句柄或 <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc">RasDialFunc</see>、
    /// <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc1">RasDialFunc1</see> 或 
    /// <see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nc-ras-rasdialfunc2">RasDialFunc2</see>
    /// 回调函数以接收 RasDial 事件通知。
    /// <paramref name="dwNotifierType"/> 参数指定 <paramref name="lpvNotifier"/> 的性质。
    /// 有关更多详细信息，请参阅前面的说明。</para>
    /// <para>如果此参数不为 <b>NULL</b>，则 <b>RasDial</b> 会为每个 <b>RasDial</b> 事件向窗口发送消息或调用回调函数。
    /// 此外，<b>RasDial</b> 调用以异步方式运行：<b>RasDial</b> 在建立连接之前立即返回，并通过窗口或回调函数传达其进度。</para>
    /// <para>如果 <paramref name="lpvNotifier"/> 为 NULL，则 <b>RasDial</b> 调用将同步运行：<b>RasDial</b> 在连接尝试成功完成或失败之前不会返回。</para>
    /// <para>如果 <paramref name="lpvNotifier"/> 不为 NULL，则初始调用 <b>RasDial</b> 后，随时可能会向窗口或回调函数发出通知。
    /// 发生以下事件之一时，通知结束：
    /// <list type="bullet">
    /// <item>已建立连接。换句话说，RAS 连接状态 RASCS_Connected。</item>
    /// <item>连接失败。换句话说，<i>dwError</i> 是非零值。</item>
    /// <item>在连接上调用 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/api/ras/nf-ras-rashangupa">RasHangUp</see>。</item>
    /// </list>
    /// </para>
    /// </param>
    /// <param name="lphRasConn">
    /// 指向 <b>HRASCONN</b> 类型的变量的指针。
    /// 在调用 <b>RasDial</b> 之前，将 <b>HRASCONN</b> 变量设置为 <b>NULL</b>。
    /// 如果 <b>RasDial</b> 成功，它将 RAS 连接的句柄存储到 <paramref name="lphRasConn"/>。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b> ，并在 <paramref name="lphRasConn"/> 指向的变量中返回 RAS 连接的句柄。</para>
    /// <para>如果函数失败，则返回值为“<see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see>”或“Winerror.h”。</para>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasDial(
        IntPtr lpRasDialExtensions,
        string? lpszPhonebook,
        in RASDIALPARAMS lprasdialparams,
        uint dwNotifierType,
        IntPtr? lpvNotifier,
        out IntPtr lphRasConn);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rashangupw">RasHangUp</see></b>
    /// 函数终止远程访问连接。使用 RAS 连接句柄指定连接。该函数释放与句柄关联的所有 RASAPI32.DLL 资源。
    /// </summary>
    /// <param name="hRasConn">
    /// 指定要终止的远程访问连接。 这是从上一次调用 rasDial 或 RasEnumConnections 返回的句柄。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值是以下错误代码之一或来自 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see> 或 WinError.h 的值。</para>
    /// <list type="table">
    /// <item>
    /// <term>ERROR_INVALID_HANDLE</term>
    /// <description><paramref name="hRasConn"/> 中指定的句柄无效。</description>
    /// </item>
    /// </list>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasHangUp(
        IntPtr hRasConn);

    /// <summary>
    /// <b><see href="https://learn.microsoft.com/zh-cn/windows/win32/api/ras/nf-ras-rasgeterrorstringw">RasGetErrorString</see></b>
    /// 函数获取指定 RAS 错误值的错误消息字符串。
    /// </summary>
    /// <param name="uErrorValue">
    /// 指定感兴趣的错误值。这些值由 RAS 函数之一返回：RasError.h 头文件中列出的值。
    /// </param>
    /// <param name="lpszErrorString">
    /// 指向接收错误字符串的缓冲区的指针。此参数不得 NULL。
    /// </param>
    /// <param name="cBufSize">
    /// 指定由 <paramref name="lpszErrorString"/> 指向的缓冲区的大小（以字符为单位）。
    /// </param>
    /// <returns>
    /// <para>如果函数成功，则返回值 <b>ERROR_SUCCESS</b>。</para>
    /// <para>如果函数失败，则返回值是以下错误代码之一或来自 <see href="https://learn.microsoft.com/zh-cn/windows/desktop/RRAS/routing-and-remote-access-error-codes">路由和远程访问错误代码</see> 或 WinError.h 的值。
    /// <b>RasGetErrorString</b> 函数未设置 <see href="https://learn.microsoft.com/zh-cn/previous-versions/windows/desktop/wab/-wab-iabcontainer-getlasterror">GetLastError</see> 信息。</para>
    /// <list type="table">
    /// <item>
    /// <term>ERROR_INVALID_PARAMETER</term>
    /// <description>将无效参数传递到函数中。</description>
    /// </item>
    /// </list>
    /// </returns>
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasGetErrorString(
        uint uErrorValue,
        [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder lpszErrorString,
        uint cBufSize);

    #endregion

    /// <summary>
    /// 拨号。返回连接句柄（用于断开）。
    /// </summary>
    /// <param name="entryName"></param>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    public static void Connect(string entryName, string userName, string password)
    {
        var p = new RASDIALPARAMS
        {
            size = Marshal.SizeOf<RASDIALPARAMS>(),
            entryName = entryName,
            phoneNumber = "",
            callbackNumber = "",
            userName = userName,
            password = password,
            domain = "",
        };

        uint ret = RasDial(IntPtr.Zero, null, in p, 0, null, out var conn);
        if (ret != 0)
            throw new Win32Exception((int)ret, $"拨号连接失败: {GetErrorString(ret)}");
    }

    /// <summary>
    /// 断开指定 PPPoE 连接。
    /// </summary>
    /// <param name="entryName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="Win32Exception"></exception>
    public static void Disconnect(string entryName)
    {
        if (string.IsNullOrEmpty(entryName))
            throw new ArgumentException("输入参数必须为非空值。");

        var connection = GetActivePppoeConnection() ?? throw new InvalidOperationException("当前没有活跃的连接项目。");

        ArgumentOutOfRangeException.ThrowIfNotEqual(entryName, connection.entryName);

        uint ret = RasHangUp(connection!.handle);

        if (ret != 0)
            throw new Win32Exception((int)ret, $"断开拨号连接失败: {GetErrorString(ret)}");
    }

    /// <summary>
    /// 列出系统电话簿里的所有连接名。
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    public static List<RASENTRYNAME> ListAllEntries()
    {
        uint cb = 0;
        uint ret = RasEnumEntries(null, null, null, ref cb, out uint count);

        if (ret == ERROR_SUCCESS && count == 0)
            return [];

        if (ret != ERROR_BUFFER_TOO_SMALL)
            throw new Win32Exception((int)ret, $"获取电话簿条目失败：{GetErrorString(ret)}");

        int size = Marshal.SizeOf<RASENTRYNAME>();
        int entriesCount = (int)(cb / size);
        var buffer = new RASENTRYNAME[entriesCount];
        buffer[0].size = size;

        ret = RasEnumEntries(null, null, buffer, ref cb, out count);

        if (ret == ERROR_INVALID_SIZE)
            throw new Win32Exception((int)ret, "不是适配的操作系统平台。");

        if (ret != ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"获取电话簿条目失败：{GetErrorString(ret)}");

        List<RASENTRYNAME> result = [];
        for (int i = 0; i < count; i++)
            result.Add(buffer[i]);

        return result;
    }

    /// <summary>
    /// 获取当前已接通的 Pppoe 连接。若没有已接通的 Pppoe 连接，则返回 null。
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    public static RASCONN? GetActivePppoeConnection()
    {
        var all = ListActiveRasConnections();
        foreach (var c in all)
        {
            if (string.Equals(c.deviceType, "PPPoE", StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    private static List<RASCONN> ListActiveRasConnections()
    {
        uint cb = 0;
        uint ret = RasEnumConnections(null, ref cb, out uint count);

        if (ret == ERROR_SUCCESS && count == 0)
            return [];

        if (ret != ERROR_BUFFER_TOO_SMALL)
            throw new Win32Exception((int)ret, $"获取活跃连接失败：{GetErrorString(ret)}");

        int size = Marshal.SizeOf<RASCONN>();
        int rasCount = (int)(cb / size);
        var buffer = new RASCONN[rasCount];
        buffer[0].size = size;

        ret = RasEnumConnections(buffer, ref cb, out count);

        if (ret != ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"获取活跃连接失败：{GetErrorString(ret)}");

        List<RASCONN> result = [];
        for (int i = 0; i < count; i++)
            result.Add(buffer[i]);

        return result;
    }

    /// <summary>
    /// 创建 Entry 条目。
    /// </summary>
    /// <param name="entryName"></param>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    /// <exception cref="Win32Exception"></exception>
    public static void CreateEntry(string entryName, string userName, string password)
    {
        uint ret = RasValidateEntryName(null, entryName);

        if (ret == ERROR_INVALID_NAME)
            throw new ArgumentException("名称包含非法字符。", nameof(entryName));

        if (ret != ERROR_SUCCESS && ret != ERROR_ALREADY_EXISTS)
            throw new Win32Exception((int)ret, $"验证输入名称失败：{GetErrorString(ret)}");

        var entry = new RASENTRY
        {
            size = Marshal.SizeOf<RASENTRY>(),
            entryType = RasEntryType.Broadband,
            options = RASEO.RemoteDefaultGateway 
                | RASEO.ShowDialingProgress 
                | RASEO.PreviewUserPassword 
                | RASEO.PreviewDomain
                | RASEO.RequirePap
                | RASEO.RequireChap,
            networkProtocols = RASNP.IP | RASNP.IPv6,
            framingProtocol = RasFramingProtocol.Ppp,
            deviceType = "PPPOE",
            deviceName = "WAN Miniport (PPPOE)",
            phoneNumber = "",
            id = Guid.NewGuid(),
            encryptionType = RasEncryptionType.Optional,
        };

        ret = RasSetEntryProperties(null, entryName, in entry, (uint)entry.size, null, 0);

        if (ret != ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"创建电话簿条目失败。错误信息：{GetErrorString(ret)}");

        if (!string.IsNullOrEmpty(userName))
        {
            var cred = new RASCREDENTIALS
            {
                size = Marshal.SizeOf<RASCREDENTIALS>(),
                options = RASCM.UserName | RASCM.Password,
                userName = userName,
                password = password ?? "",
                domain = ""
            };

            ret = RasSetCredentials(null, entryName, in cred, false);
            if (ret != ERROR_SUCCESS)
                throw new Win32Exception((int)ret, $"设置凭据失败。错误信息：{GetErrorString(ret)}");
        }
    }

   /// <summary>
   /// 删除指定 Entry 条目。
   /// </summary>
   /// <param name="entryName"></param>
   /// <exception cref="Win32Exception"></exception>
    public static void RemoveEntry(string entryName)
    {
        uint ret = RasDeleteEntry(null, entryName);
        if (ret != ERROR_SUCCESS)
            throw new Win32Exception((int)ret, $"删除 Entry 条目失败。错误信息：{GetErrorString(ret)}");
    }

    /// <summary>
    /// 判断条目是否已存在
    /// </summary>
    /// <param name="entryName"></param>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    public static bool EntryExists(string entryName)
    {
        uint ret = RasValidateEntryName(null, entryName);
        if (ret == ERROR_ALREADY_EXISTS)
            return true;

        if (ret == ERROR_CANNOT_FIND_PHONEBOOK_ENTRY)
            return false;

        throw new Win32Exception((int)ret, GetErrorString(ret));
    }

    /// <summary>
    /// 检查是否已拨上（任意 PPP 接口 Up）。
    /// </summary>
    /// <returns></returns>
    //public static bool IsConnected()
    //{
    //    return System.Net.NetworkInformation.NetworkInterface
    //        .GetAllNetworkInterfaces()
    //        .Any(n => n.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ppp
    //               && n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up);
    //}

    /// <summary>
    /// 错误码转文本。
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static string GetErrorString(uint code)
    {
        var stringBuilder = new System.Text.StringBuilder(512);
        uint ret = RasGetErrorString(code, stringBuilder, 512);

        if (ret != 0)
            return $"未知错误 (0x{code:X8})";

        return stringBuilder.ToString();
    }
}