using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static NetSeal.Core.Services.Ras.NativeMethods;

namespace NetSeal.Core.Services.Ras;

public interface IRasDialer
{
    /// <summary>
    /// 在指定电话簿中创建条目。
    /// 当条目存在时，会跳过创建。
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 若要使用当前默认的电话簿，请将此参数设为 null。
    /// </param>
    /// <param name="entryName">
    /// 条目名称。
    /// </param>
    /// <param name="username">
    /// 条目用户名。
    /// 若此参数为 null，则必须在 <see cref="RasConnectAsync(string?, string, string, string, CancellationToken)"/> 中输入此凭据。
    /// </param>
    /// <param name="password">
    /// 条目密码。
    /// 若此参数为 null，则必须在 <see cref="RasConnectAsync(string?, string, string, string, CancellationToken)"/> 中输入此凭据。
    /// </param>
    /// <returns></returns>
    public Task RasCreateEntryAsync(string? phoneBook, string entryName, string? username, string? password);

    /// <summary>
    /// 移除指定电话簿中的指定条目。
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 若要使用当前默认的电话簿，请将此参数设为 null。
    /// </param>
    /// <param name="entryName">
    /// 条目名称。
    /// </param>
    /// <returns></returns>
    public Task RasRemoveEntryAsync(string? phoneBook, string entryName);

    /// <summary>
    /// 获取当前活跃状态的连接。
    /// </summary>
    /// <returns></returns>
    public Task<RasConnection?> GetActiveRasConnectionAsync();

    /// <summary>
    /// 连接指定电话簿中的指定条目。
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 若要使用当前默认的电话簿，请将此参数设为 null。
    /// </param>
    /// <param name="entryName">
    /// 条目名称。
    /// </param>
    /// <param name="username">
    /// 条目用户名。
    /// 若此参数为 null，则必须在 <see cref="RasCreateEntryAsync(string?, string, string?, string?)"/> 中输入此凭据。
    /// </param>
    /// <param name="password">
    /// 条目密码。
    /// 若此参数为 null，则必须在 <see cref="RasCreateEntryAsync(string?, string, string?, string?)"/> 中输入此凭据。
    /// </param>
    /// <param name="cancellationToken">
    /// 取消令牌。
    /// </param>
    /// <returns></returns>
    /// <exception cref="TaskCanceledException"></exception>
    public Task RasConnectAsync(string? phoneBook, string entryName, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>断开指定电话簿中的指定条目。</para>
    /// <para>请注意，如果条目连接不是在当前进程会话创建的（比如在设置中创建的连接，或自上次连接以来此应用有重启），
    /// 那么 <see cref="RasDisconnectAsync(string?, string, CancellationToken)"/> 将不会执行断开操作。
    /// 请调用 <see cref="GetIsConnectionManagedByCurrentSessionAsync()"/> 或 
    /// <see cref="IsConnectionManagedByCurrentSession(RasConnection)"/> 
    /// 来判断连接是否在当前进程会话下创建，
    /// 并在方法返回 false 时，使用其他方法来断开此连接（例如配置此连接的网络适配器）。</para>
    /// </summary>
    /// <param name="phoneBook">
    /// 该字符串指定电话簿（PBK）文件的完整路径和文件名。
    /// 若要使用当前默认的电话簿，请将此参数设为 null。
    /// </param>
    /// <param name="entryName">
    /// 条目名称。
    /// </param>
    /// <param name="cancellationToken">
    /// 取消令牌。
    /// </param>
    /// <returns></returns>
    /// <exception cref="TaskCanceledException"></exception>
    public Task RasDisconnectAsync(string? phoneBook, string entryName, CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>检查条目连接是否在当前进程会话下创建。</para>
    /// <para>请注意，如果条目连接不是在当前进程会话创建的（比如在设置中创建的连接，或自上次连接以来此应用有重启），
    /// 那么 <see cref="RasDisconnectAsync(string?, string, CancellationToken)"/> 将不会执行断开操作。
    /// 请调用 <see cref="GetIsConnectionManagedByCurrentSessionAsync()"/> 或 
    /// <see cref="IsConnectionManagedByCurrentSession(RasConnection)"/> 
    /// 来判断连接是否在当前进程会话下创建，
    /// 并在方法返回 false 时，使用其他方法来断开此连接（例如配置此连接的网络适配器）。</para>
    /// <para>调用此方法负载时，必须保证当前有活跃状态的连接，否则方法将引发异常 <see cref="InvalidOperationException"/></para>
    /// </summary>
    /// <returns>
    /// 如果连接由当前进程会话创建和管理，则返回 true；否则，返回 false。
    /// </returns>
    /// <exception cref="InvalidOperationException">调用此方法负载时，若不存在活跃连接，则会引发此异常</exception>
    public Task<bool> GetIsConnectionManagedByCurrentSessionAsync();

    /// <summary>
    /// <para>检查条目连接是否在当前进程会话下创建。</para>
    /// <para>请注意，如果条目连接不是在当前进程会话创建的（比如在设置中创建的连接，或自上次连接以来此应用有重启），
    /// 那么 <see cref="RasDisconnectAsync(string?, string, CancellationToken)"/> 将不会执行断开操作。
    /// 请调用 <see cref="GetIsConnectionManagedByCurrentSessionAsync()"/> 或 
    /// <see cref="IsConnectionManagedByCurrentSession(RasConnection)"/> 
    /// 来判断连接是否在当前进程会话下创建，
    /// 并在方法返回 false 时，使用其他方法来断开此连接（例如配置此连接的网络适配器）。</para>
    /// </summary>
    /// <returns>
    /// 如果连接由当前进程会话创建和管理，则返回 true；否则，返回 false。
    /// </returns>
    public Task<bool> IsConnectionManagedByCurrentSession(RasConnection connection);
}
