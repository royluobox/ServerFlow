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
    private const string AppUserModelId = "ServerFlow.Scheduler_pbhj1ej857fh2!App";

    [STAThread]
    private static int Main(string[] args)
    {
        var quiet = args != null && args.Any(a => string.Equals(a, "/quiet", StringComparison.OrdinalIgnoreCase));

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

        var workDir = Path.Combine(Path.GetTempPath(), "ServerFlowSetup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        using (var progress = new ProgressForm())
        {
            if (!quiet)
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

                progress.SetStatus("正在信任应用签名证书…");
                TrustCertificate(cer);

                progress.SetStatus("正在安装 ServerFlow 服务器排班…");
                var result = RunPowerShell(BuildInstallScript(msix), 900000);
                if (!result.ok)
                {
                    throw new InvalidOperationException(result.message);
                }

                if (quiet)
                {
                    return 0;
                }

                progress.SetStatus("安装完成，正在启动…");
                progress.Hide();

                MessageBox.Show("ServerFlow 服务器排班 已安装完成。\n\n可从开始菜单打开，已为你启动。",
                    "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                Launch();
                return 0;
            }
            catch (Exception ex)
            {
                if (!quiet)
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
                try
                {
                    if (Directory.Exists(workDir))
                    {
                        Directory.Delete(workDir, true);
                    }
                }
                catch
                {
                    // 临时文件清理失败不影响安装结果
                }
            }
        }
    }

    private static void Launch()
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

    private static (bool ok, string message) RunPowerShell(string script, int timeoutMs)
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

                return (false, "安装超时，请重试。");
            }

            var text = (outputTask.Result + Environment.NewLine + errorTask.Result).Trim();
            if (process.ExitCode == 0)
            {
                return (true, text);
            }

            var message = text.Replace("ERROR:", string.Empty).Trim();
            if (message.Length == 0)
            {
                message = "系统返回错误码 " + process.ExitCode + "。";
            }

            return (false, message);
        }
    }

    private sealed class ProgressForm : Form
    {
        private readonly Label _status;

        public ProgressForm()
        {
            Text = "ServerFlow 安装程序";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(440, 112);
            BackColor = Color.White;

            _status = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(24, 26, 392, 24),
                Text = "正在准备安装…",
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(32, 32, 32)
            };

            var bar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 28,
                Bounds = new Rectangle(24, 60, 392, 16)
            };

            Controls.Add(_status);
            Controls.Add(bar);
        }

        public void SetStatus(string text)
        {
            _status.Text = text;
            _status.Refresh();
            Application.DoEvents();
        }
    }
}
