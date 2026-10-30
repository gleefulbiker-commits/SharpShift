using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SharpShift.Inventory.DependencyAnalysis;

internal sealed class HttpRequestExecutor(HttpClient httpClient, DependencyHealthCheckOptions options)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly DependencyHealthCheckOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.RequestTimeout);

            try
            {
                var request = requestFactory();
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
                if ((int)response.StatusCode >= 500 && attempt == 0)
                {
                    response.Dispose();
                    continue;
                }

                return response;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt == 0)
            {
                lastException = new TimeoutException($"Dependency health request timed out after {_options.RequestTimeout}.");
            }
            catch (HttpRequestException ex) when (attempt == 0)
            {
                lastException = ex;
            }
        }

        throw lastException ?? new HttpRequestException("Dependency health request failed.");
    }
}
