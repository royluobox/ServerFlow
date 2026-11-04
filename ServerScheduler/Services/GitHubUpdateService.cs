using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
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
    private const string LatestReleaseApi = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";
    private const string ReleasePage = $"https://github.com/{Owner}/{Repository}/releases/latest";

    public const string AppInstallerUrl =
        $"https://github.com/{Owner}/{Repository}/releases/latest/download/ServerFlow.appinstaller";

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public GitHubUpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ServerFlow-UpdateChecker/1.0");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 1, 0);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(LatestReleaseApi, cancellationToken);
            response.EnsureSuccessStatusCode();

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
                return new UpdateCheckResult(false, false, CurrentVersion, null, tag, releaseUrl,
                    $"无法解析 Release 版本号：{tag}");
            }

            return new UpdateCheckResult(true, latestVersion > CurrentVersion, CurrentVersion,
                latestVersion, tag, releaseUrl, null);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, false, CurrentVersion, null, null, ReleasePage, ex.Message);
        }
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
