using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace SharpShift.Inventory.Core.Licensing;

public sealed record LicenseAuthorizationOptions(
    Uri PrimaryStatusUri,
    Uri SecondaryStatusUri,
    Uri VerificationEndpoint,
    string LicenseFilePath,
    TimeSpan RequestTimeout,
    TimeSpan RunTokenLifetime);

public sealed record RunTokenRequest(string LicenseId, string RunNonce, LicenseDocument License);

public sealed class LicenseAuthorizationException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class LicenseAuthorizationClient : ILicenseAuthorizationClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LicenseAuthorizationOptions _options;
    private readonly LicenseVerifier _verifier;
    private readonly LicenseValidator _validator;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly bool _ownsVerifier;

    public LicenseAuthorizationClient(
        LicenseAuthorizationOptions options,
        LicenseVerifier verifier,
        HttpClient? httpClient = null,
        bool ownsVerifier = false)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _validator = new LicenseValidator(verifier);
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        _ownsVerifier = ownsVerifier;

        if (options.RequestTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Request timeout must be positive.");
        if (options.RunTokenLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Run-token lifetime must be positive.");

        _httpClient.Timeout = options.RequestTimeout;
    }

    public async Task<AuthorizedRun> AuthorizeRunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        LicenseDocument license;
        try
        {
            var licenseJson = await File.ReadAllTextAsync(_options.LicenseFilePath, cancellationToken).ConfigureAwait(false);
            license = JsonSerializer.Deserialize<LicenseDocument>(licenseJson, JsonOptions)
                ?? throw new LicenseAuthorizationException("The license file is empty or invalid.");
        }
        catch (LicenseAuthorizationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new LicenseAuthorizationException("Unable to read the license file.", ex);
        }

        if (!_validator.IsLicenseValid(license, now, "inventory"))
            throw new LicenseAuthorizationException("The license is invalid, expired, or does not include inventory scope.");

        await VerifyCurrentStatusAsync(license.LicenseId, now, cancellationToken).ConfigureAwait(false);

        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var request = new RunTokenRequest(license.LicenseId, nonce, license);
        RunToken token;
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                new Uri(_options.VerificationEndpoint, "run-tokens"), request, JsonOptions, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            token = await response.Content.ReadFromJsonAsync<RunToken>(JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new LicenseAuthorizationException("The verification endpoint returned an empty run token.");
        }
        catch (LicenseAuthorizationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new LicenseAuthorizationException("The verification endpoint could not authorize this run.", ex);
        }

        if (!_validator.IsRunTokenValid(token, license.LicenseId, nonce, now, _options.RunTokenLifetime))
            throw new LicenseAuthorizationException("The verification endpoint returned an invalid or expired run token.");

        return new AuthorizedRun(license, token);
    }

    public async Task<SignedOutputReceipt> SignOutputAsync(
        AuthorizedRun run,
        ReadOnlyMemory<byte> inventoryBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        var request = new OutputSignatureRequest(
            run.License.LicenseId,
            run.Token.RunNonce,
            Convert.ToHexString(SHA256.HashData(inventoryBytes.Span)).ToLowerInvariant());

        SignedOutputReceipt receipt;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.VerificationEndpoint, "output-signatures"))
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            var serializedToken = JsonSerializer.SerializeToUtf8Bytes(run.Token, JsonOptions);
            var bearerToken = Convert.ToBase64String(serializedToken);
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            receipt = await response.Content.ReadFromJsonAsync<SignedOutputReceipt>(JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new LicenseAuthorizationException("The verification endpoint returned an empty output signature.");
        }
        catch (LicenseAuthorizationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new LicenseAuthorizationException("The verification endpoint could not sign the inventory output.", ex);
        }

        if (!_validator.IsOutputReceiptValid(receipt, request, run.License.CustomerId, DateTimeOffset.UtcNow))
            throw new LicenseAuthorizationException("The verification endpoint returned an invalid output signature.");

        return receipt;
    }

    private async Task VerifyCurrentStatusAsync(string licenseId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var endpoints = new[] { _options.PrimaryStatusUri, _options.SecondaryStatusUri, _options.VerificationEndpoint };
        Exception? lastFailure = null;

        foreach (var endpoint in endpoints)
        {
            try
            {
                var statusUri = new Uri(endpoint, $"status/{Uri.EscapeDataString(licenseId)}");
                var status = await _httpClient.GetFromJsonAsync<SignedLicenseStatusDocument>(statusUri, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (status is not null && _validator.IsStatusValid(status, licenseId, now))
                    return;

                lastFailure = new LicenseAuthorizationException($"Status endpoint '{endpoint}' returned a stale, invalid, or non-active status document.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                lastFailure = ex;
            }
        }

        throw new LicenseAuthorizationException("No configured status endpoint returned a valid active status for this license.", lastFailure);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
        if (_ownsVerifier)
            _verifier.Dispose();
    }
}

public sealed record AuthorizedRun(LicenseDocument License, RunToken Token);
