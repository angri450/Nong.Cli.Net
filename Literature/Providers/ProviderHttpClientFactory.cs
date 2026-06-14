using System.Net;

namespace Angri450.Nong.Literature.Providers;

public static class ProviderHttpClientFactory
{
    /// <summary>User-Agent version for all Nong Literature HTTP requests.
    /// Bump when the Literature module's API contract changes.</summary>
    public static string LiteratureVersion { get; set; } = "4.1.9";

    private static string UserAgentProduct(string providerName)
        => $"Nong-Literature/{LiteratureVersion} ({providerName})";

    public static HttpClient Create(string providerName)
    {
        return Create(providerName, handler: null, baseAddress: null, timeout: null);
    }

    public static HttpClient Create(
        string providerName,
        HttpMessageHandler? handler,
        Uri? baseAddress = null,
        TimeSpan? timeout = null)
    {
        var client = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: false);

        client.Timeout = timeout ?? TimeSpan.FromSeconds(20);
        client.BaseAddress = baseAddress;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentProduct(providerName));
        return client;
    }

    public static HttpClient CreateWithTimeout(
        string providerName,
        TimeSpan timeout)
    {
        var client = new HttpClient
        {
            Timeout = timeout
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentProduct(providerName));
        return client;
    }

    internal static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await client.SendAsync(CloneRequest(request), cancellationToken).ConfigureAwait(false);
                if (!IsTransient(response.StatusCode) || attempt == maxAttempts)
                    return response;

                response.Dispose();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < maxAttempts)
            {
                lastException = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken).ConfigureAwait(false);
        }

        throw lastException ?? new HttpRequestException("Provider request failed after retries.");
    }

    static bool IsTransient(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code == 429 || code >= 500;
    }

    static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
