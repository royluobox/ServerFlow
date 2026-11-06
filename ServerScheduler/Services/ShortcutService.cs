using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;

namespace ServerScheduler.Services;

/// <summary>
/// 桌面快捷方式：图标取自应用包内 Assets\App.ico，文件名带版本号，
/// 避免 Windows 图标缓存按路径命中而一直显示旧图标。
/// </summary>
public static class ShortcutService
{
    private const string ShortcutName = "ServerFlow 服务器排班.lnk";
    private const string AppUserModelId = "ServerFlow.Scheduler_pbhj1ej857fh2!App";

    private static readonly string RootFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ServerFlow");

    private static string PendingFlag => Path.Combine(RootFolder, "shortcut-pending.flag");

    public static string Version =>
        Package.Current.Id.Version is var v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    /// <summary>更新前调用：记下“重启后重建桌面快捷方式”的意图。</summary>
    public static void RequestRebuildAfterUpdate()
    {
        try
        {
            Directory.CreateDirectory(RootFolder);
            File.WriteAllText(PendingFlag, DateTimeOffset.Now.ToString("O"));
        }
        catch
        {
            // 标记写不进去只是少一次重建，不影响更新本身
        }
    }

    /// <summary>启动时调用：若上次更新请求过重建，则重建并清除标记。</summary>
    public static void ApplyPendingRebuild()
    {
        try
        {
            if (!File.Exists(PendingFlag))
            {
                return;
            }

            CreateOrUpdateDesktopShortcut(out _);
            File.Delete(PendingFlag);
        }
        catch
        {
            // 快捷方式失败不阻塞启动
        }
    }

    /// <summary>在桌面创建（或刷新）指向本应用的快捷方式，图标使用当前版本。</summary>
    public static bool CreateOrUpdateDesktopShortcut(out string? error)
    {
        error = null;
        try
        {
            var iconPath = ExtractVersionedIcon();
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop))
            {
                error = "找不到桌面目录";
                return false;
            }

            var shortcutPath = Path.Combine(desktop, ShortcutName);
            WriteShellLink(
                shortcutPath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                "shell:AppsFolder\\" + AppUserModelId,
                iconPath);

            SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0x0000, IntPtr.Zero, IntPtr.Zero);
            return File.Exists(shortcutPath);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? ExtractVersionedIcon()
    {
        try
        {
            // 直接用包内资源：路径本身带版本号，既绕开图标缓存，也不需要额外写文件。
            // 应用数据目录在 MSIX 里是重定向的，写出去的逻辑路径资源管理器读不到。
            var source = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "App.ico");
            return File.Exists(source) ? source : null;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteShellLink(string path, string target, string arguments, string? iconPath)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(target);
        link.SetArguments(arguments);
        link.SetDescription("ServerFlow 服务器排班");
        if (!string.IsNullOrEmpty(iconPath))
        {
            link.SetIconLocation(iconPath, 0);
        }

        ((IPersistFile)link).Save(path, true);
        Marshal.FinalReleaseComObject(link);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr data, uint flags);

        void GetIDList(out IntPtr itemIdList);

        void SetIDList(IntPtr itemIdList);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxPath);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

        void GetHotkey(out short hotkey);

        void SetHotkey(short hotkey);

        void GetShowCmd(out int showCommand);

        void SetShowCmd(int showCommand);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath,
            out int iconIndex);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);

        void Resolve(IntPtr windowHandle, uint flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);

        [PreserveSig]
        int IsDirty();

        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);

        void Save([MarshalAs(UnmanagedType.LPWStr)] string? fileName, bool remember);

        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);

        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
