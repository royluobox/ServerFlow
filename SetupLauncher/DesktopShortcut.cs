using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// 创建 .lnk。走 shell32 的 ShellLink coclass，不依赖 Windows Script Host，
/// 并对“提权后当前用户不是桌面用户”的情况定位到控制台会话用户的桌面。
/// </summary>
internal static class DesktopShortcut
{
    private const string ShortcutName = "ServerFlow 服务器排班.lnk";
    private const int WtsUserName = 5;
    private const int ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSQuerySessionInformationW(
        IntPtr server, uint sessionId, int infoClass, out IntPtr buffer, out int bytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

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
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);

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

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);

        void Resolve(IntPtr window, uint flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);

        [PreserveSig]
        int IsDirty();

        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);

        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);

        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);

        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }

    internal sealed class Result
    {
        public bool Ok { get; set; }

        public string Path { get; set; }

        public string Message { get; set; }
    }

    public static Result Create(string targetPath, string arguments, string iconPath)
    {
        var failures = new List<string>();

        foreach (var desktop in EnumerateDesktopDirectories())
        {
            var linkPath = System.IO.Path.Combine(desktop, ShortcutName);
            try
            {
                SaveLink(linkPath, targetPath, arguments, iconPath);
                if (!File.Exists(linkPath))
                {
                    throw new IOException("文件未生成");
                }

                SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
                return new Result { Ok = true, Path = linkPath };
            }
            catch (Exception ex)
            {
                failures.Add(desktop + "（" + ex.Message + "）");
            }
        }

        return new Result
        {
            Ok = false,
            Message = failures.Count == 0
                ? "未找到可写入的桌面目录"
                : string.Join("；", failures)
        };
    }

    private static void SaveLink(string linkPath, string targetPath, string arguments, string iconPath)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetPath);
        link.SetArguments(arguments);
        link.SetDescription("ServerFlow 服务器排班");

        var icon = iconPath;
        if (!string.IsNullOrEmpty(icon) && File.Exists(icon))
        {
            link.SetIconLocation(icon, 0);
        }

        ((IPersistFile)link).Save(linkPath, true);
        Marshal.FinalReleaseComObject(link);
    }

    private static IEnumerable<string> EnumerateDesktopDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usersRoot = System.IO.Path.GetDirectoryName(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        var consoleUser = TryGetConsoleUserName();
        if (!string.IsNullOrEmpty(consoleUser) && !string.IsNullOrEmpty(usersRoot))
        {
            foreach (var path in ProfileDesktops(System.IO.Path.Combine(usersRoot, consoleUser)))
            {
                if (Directory.Exists(path) && seen.Add(path))
                {
                    yield return path;
                }
            }
        }

        foreach (var path in ProfileDesktops(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            if (Directory.Exists(path) && seen.Add(path))
            {
                yield return path;
            }
        }

        var current = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrEmpty(current) && Directory.Exists(current) && seen.Add(current))
        {
            yield return current;
        }

        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (!string.IsNullOrEmpty(common) && Directory.Exists(common) && seen.Add(common))
        {
            yield return common;
        }
    }

    private static IEnumerable<string> ProfileDesktops(string profile)
    {
        if (string.IsNullOrEmpty(profile))
        {
            yield break;
        }

        yield return System.IO.Path.Combine(profile, "Desktop");
        yield return System.IO.Path.Combine(profile, "OneDrive", "Desktop");
    }

    private static string TryGetConsoleUserName()
    {
        try
        {
            var session = WTSGetActiveConsoleSessionId();
            if (session == 0xFFFFFFFF)
            {
                return null;
            }

            if (!WTSQuerySessionInformationW(IntPtr.Zero, session, WtsUserName, out var buffer, out _))
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(buffer)?.Trim();
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }
        catch
        {
            return null;
        }
    }
}
