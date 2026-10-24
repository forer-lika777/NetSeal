using System.Diagnostics;
using System.Text.Json;

namespace NetSeal.Core.Services;

/// <summary>
/// 由 UI 修改、由服务读取的共享配置。
/// <para>保存在 <c>%ProgramData%\NetSeal\config\service.json</c>。</para>
/// <para>该目录需要由安装器授予普通用户修改权限，UI 才能免提权写入。</para>
/// <para>此文件不含任何机密信息；账号密码由 <c>secrets\credentials.dat</c> 单独保存。</para>
/// </summary>
public sealed class ServiceSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 网络可用且尚未连接时自动拨号。
    /// </summary>
    public bool AutoConnect { get; set; } = true;

    /// <summary>
    /// 从睡眠/休眠恢复后立即重连，即使 <see cref="AutoConnect"/> 关闭。
    /// </summary>
    public bool ReconnectOnWake { get; set; } = true;

    /// <summary>
    /// 服务启动（含开机自启）后立即重连，即使 <see cref="AutoConnect"/> 关闭。
    /// </summary>
    public bool ReconnectOnStartup { get; set; } = true;

    /// <summary>
    /// 宽带账号。仅用于初始化拨号条目，密码不保存在此文件中。
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// 配置文件所在目录。
    /// </summary>
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "NetSeal",
        "config");

    /// <summary>
    /// 配置文件路径。
    /// </summary>
    public static string FilePath => Path.Combine(DirectoryPath, "service.json");

    /// <summary>
    /// 读取共享配置。文件不存在或内容损坏时返回默认值。
    /// </summary>
    public static ServiceSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new ServiceSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<ServiceSettings>(json, SerializerOptions) ?? new ServiceSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"读取服务配置失败，使用默认值。{ex.Message}");
            return new ServiceSettings();
        }
    }

    /// <summary>
    /// 写回共享配置。先写临时文件再替换，避免服务读到半截内容。
    /// </summary>
    /// <exception cref="IOException">目录不可写或文件被占用。</exception>
    /// <exception cref="UnauthorizedAccessException">当前用户没有写入权限。</exception>
    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);

        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, SerializerOptions));
        File.Move(temp, FilePath, overwrite: true);
    }
}
