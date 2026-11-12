using System.Security.Cryptography;
using NUnit.Framework;
using SharpShift.Inventory.Core.Licensing;
using SharpShift.Inventory.Licensing.Server;

namespace SharpShift.Inventory.Tests;

[TestFixture]
public sealed class LicenseRunServiceTests
{
    [Test]
    public async Task RunTokenAndOutputReceipt_AreOneTimePerNonce()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var verifier = new LicenseVerifier(ECDsa.Create(signingKey.ExportParameters(false)));
        var validator = new LicenseValidator(verifier);
        var store = new InMemoryReplayStore();
        var service = new LicenseRunService(signingKey, validator, store);
        var now = DateTimeOffset.UtcNow;
        var license = Sign(new LicenseDocument
        {
            LicenseId = "license-123",
            CustomerId = "customer-1",
            ContractorId = "contractor-1",
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddHours(1),
            Scopes = ["inventory"]
        }, signingKey);
        var status = Sign(new SignedLicenseStatusDocument
        {
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddMinutes(59),
            Licenses = [new LicenseStatusEntry(license.LicenseId, false)]
        }, signingKey);
        var request = new RunTokenRequest(license.LicenseId, new string('a', 64), license);

        var token = await service.IssueRunTokenAsync(request, status, now);
        Assert.That(token, Is.Not.Null);
        Assert.That(await service.IssueRunTokenAsync(request, status, now), Is.Null);

        var outputRequest = new OutputSignatureRequest(license.LicenseId, request.RunNonce, new string('b', 64));
        var receipt = await service.SignOutputAsync(token!, outputRequest, status, now);
        Assert.That(receipt, Is.Not.Null);
        Assert.That(validator.IsOutputReceiptValid(receipt!, outputRequest, license.CustomerId, now), Is.True);
        Assert.That(await service.SignOutputAsync(token!, outputRequest, status, now), Is.Null);
    }

    private static LicenseDocument Sign(LicenseDocument license, ECDsa key) =>
        license with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(license), key) };

    private static SignedLicenseStatusDocument Sign(SignedLicenseStatusDocument status, ECDsa key) =>
        status with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(status), key) };

    private sealed class InMemoryReplayStore : IRunTokenReplayStore
    {
        private readonly Dictionary<string, RunGrant> _grants = new(StringComparer.Ordinal);

        public Task<bool> TryReserveAsync(string runNonce, RunGrant grant, TimeSpan ttl)
        {
            if (!_grants.TryAdd(runNonce, grant))
                return Task.FromResult(false);
            return Task.FromResult(true);
        }

        public Task<RunGrant?> ConsumeAsync(string runNonce)
        {
            var found = _grants.Remove(runNonce, out var grant);
            return Task.FromResult(found ? grant : null);
        }
    }
}
