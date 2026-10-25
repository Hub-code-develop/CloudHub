using System.Runtime.InteropServices;

namespace CloudHub.Core;

/// <summary>
/// 跨平台的应用数据目录解析（配置、令牌缓存、日志）。
/// </summary>
public static class CloudHubPaths
{
    static CloudHubPaths()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            root = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(home, "AppData", "Roaming")
                : Path.Combine(home, ".config");
        }

        DataDirectory = Path.Combine(root, "CloudHub");
        Directory.CreateDirectory(DataDirectory);
    }

    /// <summary>例如 ~/.config/CloudHub（macOS/Linux）或 %APPDATA%\CloudHub（Windows）。</summary>
    public static string DataDirectory { get; }

    public static string TokenCacheFile => Path.Combine(DataDirectory, "auth.json");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string LogFile => Path.Combine(DataDirectory, "cloudhub.log");

    /// <summary>把文件权限收紧到仅当前用户可读写（含令牌的文件必须这么做）。</summary>
    public static void RestrictToCurrentUser(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // 某些文件系统（如挂载的网络盘）不支持 chmod，忽略即可。
        }
    }
}
