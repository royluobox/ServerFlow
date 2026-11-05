using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ServerScheduler.Services;

/// <summary>
/// 应用内更新：下载新版 MSIX，交给一个脱离本进程的 PowerShell 助手完成安装并重启应用。
/// 更新同一发布者的已安装包不需要管理员权限，因此全程无 UAC。
/// </summary>
public static class PackageUpdater
{
    private const string PackageFamilyName = "ServerFlow.Scheduler_pbhj1ej857fh2";
    private const string AppUserModelId = PackageFamilyName + "!App";
    private const string ProcessName = "ServerScheduler";

    public static string UpdateFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ServerFlow",
        "update");

    public static string LogPath => Path.Combine(UpdateFolder, "update.log");

    public static async Task<string> DownloadAsync(
        Uri packageUri,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(UpdateFolder);
        CleanupPreviousDownloads();

        var fileName = Path.GetFileName(packageUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "ServerFlow_update.msix";
        }

        var target = Path.Combine(UpdateFolder, fileName);
        var partial = target + ".part";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ServerFlow-Updater/1.0");

        using var response = await http.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var file = File.Create(partial))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;
                if (total > 0)
                {
                    progress?.Report((double)received / total.Value);
                }
            }
        }

        if (total > 0 && new FileInfo(partial).Length != total.Value)
        {
            throw new IOException("下载不完整，请重试。");
        }

        File.Move(partial, target, overwrite: true);
        progress?.Report(1);
        return target;
    }

    /// <summary>校验安装包哈希；拿不到清单时不阻断，只记录。</summary>
    public static async Task VerifyAsync(string packagePath, Uri? checksumsUri, CancellationToken cancellationToken)
    {
        if (checksumsUri is null)
        {
            return;
        }

        string? expected = null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var text = await http.GetStringAsync(checksumsUri, cancellationToken);
            var name = Path.GetFileName(packagePath);
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && string.Equals(parts[1].TrimStart('*').Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    expected = parts[0].Trim();
                    break;
                }
            }
        }
        catch
        {
            return;
        }

        if (expected is null)
        {
            return;
        }

        await using var stream = File.OpenRead(packagePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("安装包校验失败，已中止更新。");
        }
    }

    /// <summary>
    /// 启动等待本进程退出的助手脚本：安装 MSIX → 重新打开应用。返回后调用方应立即退出。
    /// </summary>
    public static void StartInstallerAndExit(string packagePath)
    {
        Directory.CreateDirectory(UpdateFolder);

        var script = string.Join("\r\n", new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "$log = '" + Escape(LogPath) + "'",
            "function Write-Log([string]$m) { try { Add-Content -LiteralPath $log -Value ((Get-Date).ToString('s') + ' ' + $m) } catch { } }",
            "try {",
            "    for ($i = 0; $i -lt 240; $i++) {",
            "        if (-not (Get-Process -Name '" + ProcessName + "' -ErrorAction SilentlyContinue)) { break }",
            "        Start-Sleep -Milliseconds 250",
            "    }",
            "    Write-Log ('installing ' + '" + Escape(packagePath) + "')",
            "    Add-AppxPackage -Path '" + Escape(packagePath) + "' -ForceUpdateFromAnyVersion -ForceApplicationShutdown",
            "    Write-Log 'installed'",
            "} catch {",
            "    Write-Log ('failed: ' + $_.Exception.Message)",
            "}",
            "Start-Sleep -Milliseconds 500",
            "Start-Process -FilePath 'explorer.exe' -ArgumentList 'shell:AppsFolder\\" + AppUserModelId + "'",
        });

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");

        Process.Start(new ProcessStartInfo
        {
            FileName = File.Exists(powershell) ? powershell : "powershell.exe",
            Arguments = "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " + encoded,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static void CleanupPreviousDownloads()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(UpdateFolder))
            {
                if (file.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // 旧文件删除失败不影响本次更新
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
