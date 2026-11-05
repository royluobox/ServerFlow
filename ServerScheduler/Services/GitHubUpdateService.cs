using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Windows.System;

namespace ServerScheduler.Services;

public sealed record UpdateCheckResult(
    bool Success,
    bool IsAvailable,
    Version CurrentVersion,
    Version? LatestVersion,
    string? TagName,
    string? ReleaseUrl,
    string? Error);

public sealed class GitHubUpdateService
{
    private const string Owner = "royluobox";
    private const string Repository = "ServerFlow";

    // raw.githubusercontent.com 不做匿名限流，作为主要版本来源。
    private const string RawAppInstaller =
        $"https://raw.githubusercontent.com/{Owner}/{Repository}/main/tools/installer-template/ServerFlow.appinstaller";

    // GitHub REST API 对匿名请求按 IP 限流（每小时 60 次），仅作为后备。
    private const string LatestReleaseApi = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";
    private const string ReleasePage = $"https://github.com/{Owner}/{Repository}/releases/latest";
    public const string SetupDownloadUrl =
        $"https://github.com/{Owner}/{Repository}/releases/latest/download/ServerFlow-Setup.exe";

    public const string AppInstallerUrl = RawAppInstaller;

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    public GitHubUpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ServerFlow-UpdateChecker/1.0");
    }

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var fromManifest = await TryCheckFromManifestAsync(cancellationToken);
        if (fromManifest is not null)
        {
            return fromManifest;
        }

        var fromApi = await TryCheckFromApiAsync(cancellationToken);
        if (fromApi is not null)
        {
            return fromApi;
        }

        return new UpdateCheckResult(false, false, CurrentVersion, null, null, ReleasePage,
            "暂时无法获取版本信息，请检查网络连接后重试。");
    }

    /// <summary>
    /// 读取仓库中 .appinstaller 的版本号。该文件每次发版都会更新，且不受 API 限流影响。
    /// </summary>
    private async Task<UpdateCheckResult?> TryCheckFromManifestAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var response = await _httpClient.GetAsync($"{RawAppInstaller}?t={cacheBuster}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = XDocument.Parse(xml);
            var mainPackage = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "MainPackage");
            var rawVersion = mainPackage?.Attribute("Version")?.Value
                ?? document.Root?.Attribute("Version")?.Value;

            if (rawVersion is null || !TryParseVersion(rawVersion, out var latestVersion))
            {
                return null;
            }

            var tag = "v" + latestVersion.ToString(3);
            return new UpdateCheckResult(true, latestVersion > CurrentVersion, CurrentVersion,
                latestVersion, tag, BuildReleaseUrl(tag), null);
        }
        catch
        {
            return null;
        }
    }

    private async Task<UpdateCheckResult?> TryCheckFromApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString() ?? string.Empty
                : string.Empty;
            var releaseUrl = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString() ?? ReleasePage
                : ReleasePage;

            if (!TryParseTag(tag, out var latestVersion))
            {
                return null;
            }

            return new UpdateCheckResult(true, latestVersion > CurrentVersion, CurrentVersion,
                latestVersion, tag, releaseUrl, null);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> LaunchSetupDownloadAsync()
    {
        return await Launcher.LaunchUriAsync(new Uri(SetupDownloadUrl));
    }

    public static async Task<bool> LaunchAppInstallerAsync()
    {
        var uri = new Uri("ms-appinstaller:?source=" + Uri.EscapeDataString(AppInstallerUrl));
        return await Launcher.LaunchUriAsync(uri);
    }

    public static async Task<bool> LaunchReleasePageAsync(string? releaseUrl = null)
    {
        return await Launcher.LaunchUriAsync(new Uri(releaseUrl ?? ReleasePage));
    }

    private static string BuildReleaseUrl(string tag) =>
        $"https://github.com/{Owner}/{Repository}/releases/tag/{tag}";

    private static bool TryParseVersion(string value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        return Version.TryParse(value.Trim(), out version!);
    }

    private static bool TryParseTag(string tag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        var value = tag.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var prereleaseIndex = value.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            value = value[..prereleaseIndex];
        }

        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 4)
        {
            return false;
        }

        while (parts.Length < 4)
        {
            parts = [.. parts, "0"];
        }

        return Version.TryParse(string.Join('.', parts), out version!);
    }
}
