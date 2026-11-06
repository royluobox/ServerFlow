using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows.Forms;

internal static class Program
{
    private const string PackageName = "ServerFlow.Scheduler";
    private const string PackageFamilyName = "ServerFlow.Scheduler_pbhj1ej857fh2";
    private const string AppUserModelId = PackageFamilyName + "!App";

    [STAThread]
    private static int Main(string[] args)
    {
        var quiet = args != null && args.Any(a => string.Equals(a, "/quiet", StringComparison.OrdinalIgnoreCase));
        var forceShortcut = args != null && args.Any(a => string.Equals(a, "/shortcut", StringComparison.OrdinalIgnoreCase));
        var renderPath = GetArgumentValue(args, "/render");
        var forcedState = GetArgumentValue(args, "/state");
        var forcedPage = GetArgumentValue(args, "/page");
        var forcedDpi = GetArgumentValue(args, "/dpi");

        if (Environment.OSVersion.Version.Major < 10)
        {
            if (!quiet)
            {
                MessageBox.Show("ServerFlow 需要 Windows 10 1809 或更高版本。", "ServerFlow 安装程序",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return 1;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var installed = QueryInstalled();

        if (quiet)
        {
            return Install(installed, forceShortcut, null, out _, out _) ? 0 : 1;
        }

        if (renderPath != null)
        {
            try
            {
                var previewState = string.Equals(forcedState, "none", StringComparison.OrdinalIgnoreCase)
                    ? InstalledPackage.NotInstalled
                    : (string.Equals(forcedState, "installed", StringComparison.OrdinalIgnoreCase)
                        ? new InstalledPackage(true, TargetVersion, @"C:\Program Files\WindowsApps\ServerFlow.Scheduler_" +
                            TargetVersion + ".0_x64__pbhj1ej857fh2")
                        : installed);

                float? scaleOverride = null;
                if (float.TryParse(forcedDpi, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var dpiScale) && dpiScale > 0.5f)
                {
                    scaleOverride = dpiScale;
                }

                using (var preview = new WizardForm(previewState, scaleOverride))
                {
                    preview.Show();
                    if (forcedPage != null && int.TryParse(forcedPage, out var pageIndex))
                    {
                        preview.PreviewPage(pageIndex);
                    }

                    for (var index = 0; index < 6; index++)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(80);
                    }

                    preview.PerformLayout();
                    Application.DoEvents();
                    using (var bitmap = new Bitmap(preview.Width, preview.Height))
                    {
                        preview.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                        bitmap.Save(renderPath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    preview.Close();
                }

                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(renderPath + ".err.txt", ex.ToString());
                return 2;
            }
        }

        using (var wizard = new WizardForm(installed))
        {
            wizard.ShowDialog();
            return wizard.ExitCode;
        }
    }

    private static string GetArgumentValue(string[] args, string name)
    {
        if (args == null)
        {
            return null;
        }

        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    internal static bool Install(
        InstalledPackage installed,
        bool createShortcut,
        Action<string> report,
        out string error,
        out string warning)
    {
        error = null;
        warning = null;
        var workDir = Path.Combine(Path.GetTempPath(), "ServerFlowSetup-" + Guid.NewGuid().ToString("N").Substring(0, 8));

        try
        {
            Directory.CreateDirectory(workDir);

            Report(report, "正在解压安装文件…");
            var msix = Extract("ServerFlow.msix", Path.Combine(workDir, "ServerFlow.msix"));
            var cer = Extract("ServerFlow.cer", Path.Combine(workDir, "ServerFlow-Local.cer"));

            Report(report, "正在校验并信任应用签名证书…");
            TrustCertificate(cer);

            Report(report, installed.Installed ? "正在覆盖安装…" : "正在安装 ServerFlow 服务器排班…");
            var result = RunPowerShell(BuildInstallScript(msix), 900000);
            if (!result.ok)
            {
                throw new InvalidOperationException(Summarize(result.message));
            }

            if (createShortcut)
            {
                Report(report, "正在创建桌面快捷方式…");
                var shortcut = CreateDesktopShortcut();
                if (!shortcut.Ok)
                {
                    // 安装本身成功，快捷方式失败只作为提示，不打断流程
                    var reason = Summarize(shortcut.Message);
                    if (reason.Length > 80)
                    {
                        reason = reason.Substring(0, 80) + "…";
                    }

                    warning = "桌面快捷方式未能创建（" + reason + "），可从开始菜单打开应用。";
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = Summarize(ex.Message);
            return false;
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private static void Report(Action<string> report, string message)
    {
        report?.Invoke(message);
    }

    internal static string Summarize(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "系统未返回详细原因，请重试。";
        }

        var lines = message
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("+ ", StringComparison.Ordinal))
            .ToArray();

        var text = string.Join(" ", lines);
        return text.Length <= 300 ? text : text.Substring(0, 300) + "…";
    }

    internal static void Launch()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                Arguments = "shell:AppsFolder\\" + AppUserModelId,
                UseShellExecute = true
            });
        }
        catch
        {
            // 启动失败时用户仍可从开始菜单打开
        }
    }

    internal static bool Uninstall(Action<string> report, out string message)
    {
        Report(report, "正在移除已安装的应用…");
        var script = string.Join("\r\n", new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "try {",
            "    Get-AppxPackage -Name '" + PackageName + "' | Remove-AppxPackage",
            "    Write-Output 'REMOVED'",
            "} catch {",
            "    Write-Output ('ERROR: ' + $_.Exception.Message)",
            "    exit 1",
            "}"
        });

        var result = RunPowerShell(script, 600000);
        if (!result.ok)
        {
            message = Summarize(result.message);
            return false;
        }

        Report(report, "正在清理快捷方式…");
        RemoveDesktopShortcut();
        message = "已卸载 ServerFlow。";
        return true;
    }

    private static void RemoveDesktopShortcut()
    {
        foreach (var desktop in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        })
        {
            try
            {
                if (string.IsNullOrEmpty(desktop))
                {
                    continue;
                }

                var shortcut = Path.Combine(desktop, "ServerFlow 服务器排班.lnk");
                if (File.Exists(shortcut))
                {
                    File.Delete(shortcut);
                }
            }
            catch
            {
                // 快捷方式被占用时忽略，不影响卸载结果
            }
        }

        foreach (var directory in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServerFlow"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServerFlow")
        })
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // 图标文件残留不影响卸载
            }
        }
    }

    internal static string TargetVersion
    {
        get
        {
            var metadata = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>()
                .FirstOrDefault(item => string.Equals(item.Key, "TargetAppVersion", StringComparison.Ordinal));
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Value))
            {
                return metadata.Value.Trim();
            }

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
            {
                return "1.0.0";
            }

            return version.Build > 0
                ? version.Major + "." + version.Minor + "." + version.Build
                : version.Major + "." + version.Minor;
        }
    }

    internal static Icon LoadWindowIcon()
    {
        try
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServerFlow.ico"))
            {
                return stream == null ? null : new Icon(stream);
            }
        }
        catch
        {
            return null;
        }
    }

    internal static Bitmap LoadHeaderIcon(int size)
    {
        try
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServerFlow.ico"))
            {
                if (stream == null)
                {
                    return null;
                }

                using (var icon = new Icon(stream))
                using (var source = icon.ToBitmap())
                {
                    var target = new Bitmap(size, size);
                    using (var graphics = Graphics.FromImage(target))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.DrawImage(source, new Rectangle(0, 0, size, size));
                    }

                    return target;
                }
            }
        }
        catch
        {
            return null;
        }
    }

    private static InstalledPackage QueryInstalled()
    {
        var script = string.Join("\r\n", new[]
        {
            "$p = Get-AppxPackage -Name '" + PackageName + "' | Select-Object -First 1",
            "if ($p) { $p.Version.ToString() + '|' + $p.InstallLocation } else { 'NONE' }"
        });

        var result = RunPowerShell(script, 60000);
        if (!result.ok)
        {
            return InstalledPackage.NotInstalled;
        }

        var lines = result.message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var text = lines.Length > 0 ? lines[lines.Length - 1].Trim() : string.Empty;
        if (text.Length == 0 || text.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return InstalledPackage.NotInstalled;
        }

        var parts = text.Split('|');
        var version = parts.Length > 0 ? parts[0].Trim() : string.Empty;
        var location = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        if (version.Length == 0)
        {
            return InstalledPackage.NotInstalled;
        }

        return new InstalledPackage(true, version, location);
    }

    private static string Extract(string resourceName, string targetPath)
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("安装包内容缺失：" + resourceName);
            }

            using (var file = File.Create(targetPath))
            {
                stream.CopyTo(file);
            }
        }

        return targetPath;
    }

    private static void TrustCertificate(string certificatePath)
    {
        var certificate = new X509Certificate2(certificatePath);
        using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
        {
            store.Open(OpenFlags.ReadWrite);
            var exists = store.Certificates.Cast<X509Certificate2>()
                .Any(item => string.Equals(item.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                store.Add(certificate);
            }

            store.Close();
        }
    }

    internal static DesktopShortcut.Result CreateDesktopShortcut()
    {
        var explorer = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "explorer.exe");

        return DesktopShortcut.Create(
            explorer,
            "shell:AppsFolder\\" + AppUserModelId,
            ExtractIconForShortcut());
    }

    private static string ExtractIconForShortcut()
    {
        // 放 ProgramData：跨账户可读，避免快捷方式图标在别的用户下变成空白。
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServerFlow"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServerFlow")
        };

        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServerFlow.ico"))
        {
            if (stream == null)
            {
                return null;
            }

            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();

            foreach (var directory in candidates)
            {
                try
                {
                    Directory.CreateDirectory(directory);

                    // 文件名带版本：图标缓存按路径命中，沿用同名文件会让桌面一直显示旧图标。
                    var iconPath = Path.Combine(directory, "ServerFlow-" + TargetVersion + ".ico");
                    File.WriteAllBytes(iconPath, bytes);

                    // 顺带刷新旧名，供仍指向 ServerFlow.ico 的历史快捷方式使用。
                    try
                    {
                        File.WriteAllBytes(Path.Combine(directory, "ServerFlow.ico"), bytes);
                    }
                    catch
                    {
                        // 旧名被占用不影响新图标
                    }

                    return iconPath;
                }
                catch
                {
                    // 换下一个可写目录
                }
            }
        }

        return null;
    }

    private static string BuildInstallScript(string msixPath)
    {
        return string.Join("\r\n", new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "try {",
            "    Add-AppxPackage -Path '" + msixPath.Replace("'", "''") + "' -ForceUpdateFromAnyVersion -ForceApplicationShutdown",
            "    Write-Output 'INSTALLED'",
            "} catch {",
            "    Write-Output ('ERROR: ' + $_.Exception.Message)",
            "    exit 1",
            "}"
        });
    }

    internal static (bool ok, string message) RunPowerShell(string script, int timeoutMs)
    {
        var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var powershell = Path.Combine(systemPath, @"WindowsPowerShell\v1.0\powershell.exe");
        if (!File.Exists(powershell))
        {
            powershell = "powershell.exe";
        }

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo
        {
            FileName = powershell,
            Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = Process.Start(startInfo))
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }

                return (false, "操作超时，请重试。");
            }

            var stdout = outputTask.Result.Trim();
            if (process.ExitCode == 0)
            {
                // stderr 可能带 CLIXML 进度记录，成功时只取 stdout，避免污染输出。
                return (true, stdout);
            }

            var message = (stdout + Environment.NewLine + errorTask.Result)
                .Replace("ERROR:", string.Empty).Trim();
            if (message.Length == 0)
            {
                message = "系统返回错误码 " + process.ExitCode + "。";
            }

            return (false, message);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // 临时文件清理失败不影响安装结果
        }
    }
}

internal sealed class InstalledPackage
{
    public static readonly InstalledPackage NotInstalled = new InstalledPackage(false, string.Empty, string.Empty);

    public InstalledPackage(bool installed, string version, string location)
    {
        Installed = installed;
        Version = version;
        Location = location;
    }

    public bool Installed { get; }

    public string Version { get; }

    public string Location { get; }
}
