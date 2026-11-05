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
            return Install(installed, forceShortcut, launchAfter: false, interactive: false);
        }

        if (renderPath != null)
        {
            try
            {
                using (var preview = new WizardForm(installed))
                {
                    preview.Show();
                    for (var index = 0; index < 6; index++)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(80);
                    }

                    preview.PerformLayout();
                    Application.DoEvents();
                    using (var bitmap = new Bitmap(preview.ClientSize.Width, preview.ClientSize.Height))
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
            if (wizard.ShowDialog() != DialogResult.OK)
            {
                return 0;
            }

            return Install(installed, wizard.CreateShortcut, wizard.LaunchAfterInstall, interactive: true);
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

    private static int Install(InstalledPackage installed, bool createShortcut, bool launchAfter, bool interactive)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "ServerFlowSetup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        using (var progress = new ProgressForm())
        {
            if (interactive)
            {
                progress.Show();
                progress.Refresh();
            }

            try
            {
                Directory.CreateDirectory(workDir);

                progress.SetStatus("正在解压安装文件…");
                var msix = Extract("ServerFlow.msix", Path.Combine(workDir, "ServerFlow.msix"));
                var cer = Extract("ServerFlow.cer", Path.Combine(workDir, "ServerFlow-Local.cer"));

                progress.SetStatus(installed.Installed ? "正在覆盖安装…" : "正在信任应用签名证书…");
                TrustCertificate(cer);

                progress.SetStatus("正在安装 ServerFlow 服务器排班…");
                var result = RunPowerShell(BuildInstallScript(msix), 900000);
                if (!result.ok)
                {
                    throw new InvalidOperationException(result.message);
                }

                if (createShortcut)
                {
                    progress.SetStatus("正在创建桌面快捷方式…");
                    CreateDesktopShortcut();
                }

                if (!interactive)
                {
                    return 0;
                }

                progress.Hide();
                MessageBox.Show(
                    installed.Installed
                        ? "已覆盖安装完成，排班数据保持不变。"
                        : "ServerFlow 服务器排班 已安装完成。\n\n可从开始菜单打开。",
                    "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (launchAfter)
                {
                    Launch();
                }

                return 0;
            }
            catch (Exception ex)
            {
                if (interactive)
                {
                    progress.Hide();
                    MessageBox.Show("安装失败：\n\n" + ex.Message +
                        "\n\n可访问 https://github.com/royluobox/ServerFlow 获取安装说明。",
                        "ServerFlow 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return 1;
            }
            finally
            {
                TryDeleteDirectory(workDir);
            }
        }
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

    internal static bool Uninstall(out string message)
    {
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
        message = result.ok ? "已卸载 ServerFlow。" : result.message;
        return result.ok;
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

    internal static void CreateDesktopShortcut()
    {
        var iconPath = ExtractIconForShortcut();
        var shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "ServerFlow 服务器排班.lnk");

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            return;
        }

        var shell = Activator.CreateInstance(shellType);
        var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
            new object[] { shortcutPath });
        var shortcutType = shortcut.GetType();
        shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut,
            new object[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") });
        shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut,
            new object[] { "shell:AppsFolder\\" + AppUserModelId });
        shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
            new object[] { "ServerFlow 服务器排班" });
        if (iconPath != null)
        {
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                new object[] { iconPath + ",0" });
        }

        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
    }

    private static string ExtractIconForShortcut()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ServerFlow");
            Directory.CreateDirectory(directory);
            var iconPath = Path.Combine(directory, "ServerFlow.ico");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServerFlow.ico"))
            {
                if (stream == null)
                {
                    return null;
                }

                using (var file = File.Create(iconPath))
                {
                    stream.CopyTo(file);
                }
            }

            return iconPath;
        }
        catch
        {
            return null;
        }
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
