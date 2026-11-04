using System.Net.Http.Headers;

namespace ServerScheduler.Services;

public sealed class NetworkTimeService
{
    private static readonly (string Name, string Url)[] Sources =
    [
        ("百度", "https://www.baidu.com"),
        ("Microsoft", "https://www.microsoft.com")
    ];

    private readonly HttpClient _httpClient = new();
    private TimeSpan _offset = TimeSpan.Zero;

    public DateTimeOffset Now => DateTimeOffset.Now + _offset;
    public DateTimeOffset? LastSync { get; private set; }
    public string? LastSource { get; private set; }

    public async Task<bool> SyncAsync(CancellationToken cancellationToken = default)
    {
        foreach (var source in Sources)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(4));
                using var request = new HttpRequestMessage(HttpMethod.Head, source.Url);
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                response.EnsureSuccessStatusCode();

                if (response.Headers.Date is not DateTimeOffset networkTime)
                {
                    continue;
                }

                _offset = networkTime - DateTimeOffset.Now;
                LastSync = networkTime;
                LastSource = source.Name;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Try the next public HTTPS time source. No schedule data is transmitted.
            }
        }

        return false;
    }
}
