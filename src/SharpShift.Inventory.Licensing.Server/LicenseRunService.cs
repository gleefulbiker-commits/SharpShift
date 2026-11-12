using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpShift.Inventory.Core.Licensing;
using StackExchange.Redis;

namespace SharpShift.Inventory.Licensing.Server;

public sealed record RunGrant(string LicenseId, string CustomerId, string RunNonce, string TokenSignature);

public interface IRunTokenReplayStore
{
    Task<bool> TryReserveAsync(string runNonce, RunGrant grant, TimeSpan ttl);
    Task<RunGrant?> ConsumeAsync(string runNonce);
}

public sealed class RedisRunTokenReplayStore(IDatabase database) : IRunTokenReplayStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public Task<bool> TryReserveAsync(string runNonce, RunGrant grant, TimeSpan ttl) =>
        _database.StringSetAsync(Key(runNonce), JsonSerializer.Serialize(grant, JsonOptions), ttl, When.NotExists);

    public async Task<RunGrant?> ConsumeAsync(string runNonce)
    {
        var value = await _database.StringGetDeleteAsync(Key(runNonce)).ConfigureAwait(false);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<RunGrant>((string)value!, JsonOptions);
    }

    private static string Key(string runNonce) => $"sharpshift:license:run:{runNonce}";
}

public sealed class LicenseRunService(ECDsa signingKey, LicenseValidator validator, IRunTokenReplayStore replayStore)
{
    private static readonly TimeSpan MaximumTokenLifetime = TimeSpan.FromMinutes(5);
    private readonly ECDsa _signingKey = signingKey ?? throw new ArgumentNullException(nameof(signingKey));
    private readonly LicenseValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly IRunTokenReplayStore _replayStore = replayStore ?? throw new ArgumentNullException(nameof(replayStore));

    public async Task<RunToken?> IssueRunTokenAsync(RunTokenRequest request, SignedLicenseStatusDocument status, DateTimeOffset now)
    {
        if (request is null
            || request.License is null
            || string.IsNullOrWhiteSpace(request.LicenseId)
            || !string.Equals(request.LicenseId, request.License.LicenseId, StringComparison.Ordinal)
            || !Regex.IsMatch(request.RunNonce ?? string.Empty, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)
            || !_validator.IsLicenseValid(request.License, now, "inventory")
            || !_validator.IsStatusValid(status, request.LicenseId, now))
        {
            return null;
        }

        var token = new RunToken
        {
            LicenseId = request.LicenseId,
            RunNonce = request.RunNonce!,
            IssuedAt = now,
            ExpiresAt = request.License.ExpiresAt < now.Add(MaximumTokenLifetime)
                ? request.License.ExpiresAt
                : now.Add(MaximumTokenLifetime)
        };
        token = token with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(token), _signingKey) };
        var grant = new RunGrant(request.LicenseId, request.License.CustomerId, request.RunNonce!, token.Signature);
        return await _replayStore.TryReserveAsync(request.RunNonce!, grant, token.ExpiresAt - now).ConfigureAwait(false)
            ? token
            : null;
    }

    public async Task<SignedOutputReceipt?> SignOutputAsync(
        RunToken token,
        OutputSignatureRequest request,
        SignedLicenseStatusDocument status,
        DateTimeOffset now)
    {
        if (token is null
            || request is null
            || string.IsNullOrWhiteSpace(request.OutputSha256)
            || !Regex.IsMatch(request.OutputSha256, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)
            || !_validator.IsRunTokenValid(token, request.LicenseId, request.RunNonce, now, MaximumTokenLifetime)
            || !_validator.IsStatusValid(status, request.LicenseId, now))
        {
            return null;
        }

        var grant = await _replayStore.ConsumeAsync(token.RunNonce).ConfigureAwait(false);
        if (grant is null
            || grant.LicenseId != request.LicenseId
            || grant.RunNonce != request.RunNonce
            || grant.TokenSignature != token.Signature)
        {
            return null;
        }

        var receipt = new SignedOutputReceipt
        {
            LicenseId = grant.LicenseId,
            CustomerId = grant.CustomerId,
            RunNonce = grant.RunNonce,
            OutputSha256 = request.OutputSha256,
            IssuedAt = now
        };
        return receipt with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(receipt), _signingKey) };
    }
}
