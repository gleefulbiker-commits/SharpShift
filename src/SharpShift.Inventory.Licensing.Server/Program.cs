using System.Security.Cryptography;
using System.Text.Json;
using SharpShift.Inventory.Core.Licensing;
using SharpShift.Inventory.Licensing.Server;
using SharpShift.LoggerMessages;
using StackExchange.Redis;

var publicKeyPem = RequiredSetting("SHARPSHIFT_LICENSE_PUBLIC_KEY_PEM");
var signingKeyPath = RequiredSetting("SHARPSHIFT_LICENSE_SIGNING_KEY_PATH");
var statusFilePath = RequiredSetting("SHARPSHIFT_LICENSE_STATUS_FILE");
var redisConnectionString = RequiredSetting("SHARPSHIFT_LICENSE_REDIS_CONNECTION");
var publicKey = LicenseVerifier.LoadPublicKey(publicKeyPem);
var signingKey = ECDsa.Create();
signingKey.ImportFromPem(await File.ReadAllTextAsync(signingKeyPath));
if (signingKey.KeySize != 256)
    throw new CryptographicException("The licensing signing key must use ECDSA P-256.");
var signingPublicKey = signingKey.ExportParameters(false);
var pinnedPublicKey = publicKey.ExportParameters(false);
if (!CryptographicOperations.FixedTimeEquals(signingPublicKey.Q.X!, pinnedPublicKey.Q.X!)
    || !CryptographicOperations.FixedTimeEquals(signingPublicKey.Q.Y!, pinnedPublicKey.Q.Y!))
{
    signingKey.Dispose();
    publicKey.Dispose();
    throw new CryptographicException("The server signing key does not match the pinned public key.");
}

var redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
var database = redis.GetDatabase();
var verifier = new LicenseVerifier(publicKey);
var validator = new LicenseValidator(verifier);
var replayStore = new RedisRunTokenReplayStore(database);
var runService = new LicenseRunService(signingKey, validator, replayStore);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/status/{licenseId}", async (string licenseId, HttpContext context, ILogger<Program> logger) =>
{
    try
    {
        var status = await ReadStatusAsync(statusFilePath, jsonOptions);
        if (status.Licenses is null
            || status.Licenses.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.LicenseId))
            || !verifier.VerifyStatusSignature(status))
        {
            LogMessages.LicenseInvalidStatusSignature(logger, licenseId);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        context.Response.Headers.CacheControl = "public, max-age=3600";
        LogMessages.LicenseStatusServed(logger, licenseId);
        return Results.Json(status, jsonOptions);
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
    {
        LogMessages.LicenseStatusUnavailable(logger, licenseId);
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/run-tokens", async (RunTokenRequest request, ILogger<Program> logger) =>
{
    try
    {
        var now = DateTimeOffset.UtcNow;
        var status = await ReadStatusAsync(statusFilePath, jsonOptions);
        var token = await runService.IssueRunTokenAsync(request, status, now);
        if (token is null)
        {
            LogMessages.LicenseRejectedOrReplayed(logger, request?.LicenseId ?? "unknown");
            return Results.Unauthorized();
        }

        LogMessages.LicenseAuthorized(logger, token.LicenseId);
        return Results.Json(token, jsonOptions);
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
    {
        LogMessages.LicenseStatusUnavailable(logger, request?.LicenseId ?? "unknown");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (RedisException)
    {
        logger.LogError("LicenseId {LicenseId} outcome {Outcome}", request?.LicenseId ?? "unknown", "replay-store-unavailable");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/output-signatures", async (HttpRequest httpRequest, OutputSignatureRequest request, ILogger<Program> logger) =>
{
    if (request is null || !TryReadToken(httpRequest, jsonOptions, out var token))
    {
        LogMessages.LicenseInvalidStatusSignature(logger, request?.LicenseId ?? "unknown");
        return Results.Unauthorized();
    }

    try
    {
        var status = await ReadStatusAsync(statusFilePath, jsonOptions);
        var receipt = await runService.SignOutputAsync(token, request, status, DateTimeOffset.UtcNow);
        if (receipt is null)
        {
            LogMessages.LicenseRejectedOrReplayed(logger, request.LicenseId);
            return Results.Conflict();
        }

        LogMessages.LicenseOutputSigned(logger, receipt.LicenseId);
        return Results.Json(receipt, jsonOptions);
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
    {
        LogMessages.LicenseStatusUnavailable(logger, request?.LicenseId ?? "unknown");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (RedisException)
    {
        LogMessages.LicenseReplayStoreUnavailable(logger, request?.LicenseId ?? "unknown");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

try
{
    await app.RunAsync();
}
finally
{
    signingKey.Dispose();
    verifier.Dispose();
    await redis.DisposeAsync();
}

static string RequiredSetting(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Required environment variable '{name}' is not configured.");

static async Task<SignedLicenseStatusDocument> ReadStatusAsync(string path, JsonSerializerOptions options)
{
    await using var stream = File.OpenRead(path);
    return await JsonSerializer.DeserializeAsync<SignedLicenseStatusDocument>(stream, options)
        ?? throw new JsonException("Status document is empty.");
}

static bool TryReadToken(HttpRequest request, JsonSerializerOptions options, out RunToken token)
{
    token = new RunToken();
    var authorization = request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return false;

    try
    {
        var bytes = Convert.FromBase64String(authorization[7..]);
        token = JsonSerializer.Deserialize<RunToken>(bytes, options)!;
        return token is not null;
    }
    catch (FormatException)
    {
        return false;
    }
    catch (JsonException)
    {
        return false;
    }
}

