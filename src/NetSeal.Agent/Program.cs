using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace NetSeal.Agent;

public partial class Program
{
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private static void ShowMesssage(string text, string caption, bool isError = false)
    {
        uint type = isError ? 0x10u : 0x40u; // 0x10=错误图标, 0x40=信息图标
        MessageBox(IntPtr.Zero, text, caption, type);
    }

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return ShowUsage();
        }

        string command = args[0];

        try
        {
            if (command == "--set-cred" && args.Length >= 2)
            {
                return SetCredential(args[1]);
            }
            return ShowUsage();
        }
        catch (Exception ex)
        {
            ShowMesssage($"发生错误:\n{ex.Message}", "NetSeal.Agent", true);
            return 1;
        }
    }

    private static int ShowUsage()
    {
        ShowMesssage("用法:\nNetSeal.Agent --set-cred <临时凭据文件路径>", "参数错误", true);
        return 1;
    }

    private static int SetCredential(string tempFilePath)
    {
        if (!File.Exists(tempFilePath))
        {
            ShowMesssage("找不到临时凭据文件。", "错误", true);
            return 2;
        }

        byte[] plainBytes = File.ReadAllBytes(tempFilePath);
        File.Delete(tempFilePath);

        byte[] cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);

        string rootDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NetSeal");

        // 凭据单独放在 secrets 子目录：只允许 SYSTEM 和 Administrators 访问。
        // 父目录 NetSeal 下的 config 子目录需要给普通用户写权限（UI 免提权修改服务设置），
        // 因此不能对整个 NetSeal 目录关闭权限继承。
        string dataDir = Path.Combine(rootDir, "secrets");

        Directory.CreateDirectory(dataDir);

        string targetFile = Path.Combine(dataDir, "credentials.dat");
        File.WriteAllBytes(targetFile, cipherBytes);

        try
        {
            var dirInfo = new DirectoryInfo(dataDir);
            var dirSecurity = dirInfo.GetAccessControl();

            dirSecurity.SetAccessRuleProtection(true, false);

            dirSecurity.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirSecurity.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(dirSecurity);
        }
        catch (Exception ex)
        {
            ShowMesssage($"设置文件权限失败: {ex.Message}", "警告", true);
        }

        ShowMesssage("账号密码已安全保存！", "NetSeal");
        return 0;
    }
}