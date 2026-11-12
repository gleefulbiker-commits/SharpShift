using System.Security.Cryptography;
using NUnit.Framework;
using SharpShift.Inventory.Core.Licensing;

namespace SharpShift.Inventory.Tests;

[TestFixture]
public sealed class LicenseValidatorTests
{
    [Test]
    public void IsLicenseValid_AcceptsSignedCurrentLicenseAndRejectsTamperingOrExpiry()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var verifier = new LicenseVerifier(ECDsa.Create(key.ExportParameters(false)));
        var validator = new LicenseValidator(verifier);
        var now = DateTimeOffset.UtcNow;
        var license = Sign(new LicenseDocument
        {
            LicenseId = "license-123",
            CustomerId = "customer-1",
            ContractorId = "contractor-1",
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddDays(1),
            Scopes = ["inventory"]
        }, key);

        Assert.That(validator.IsLicenseValid(license, now, "inventory"), Is.True);
        Assert.That(validator.IsLicenseValid(license with { Signature = Convert.ToBase64String([1]) }, now, "inventory"), Is.False);
        Assert.That(validator.IsLicenseValid(license with { CustomerId = "tampered" }, now, "inventory"), Is.False);
        Assert.That(validator.IsLicenseValid(Sign(license with { ExpiresAt = now.AddSeconds(-1) }, key), now, "inventory"), Is.False);
        Assert.That(validator.IsLicenseValid(license, now, "other"), Is.False);
    }

    [Test]
    public void IsStatusValid_RejectsRevokedStaleTamperedAndDuplicateEntries()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var publicKey = ECDsa.Create(key.ExportParameters(false));
        var validator = new LicenseValidator(new LicenseVerifier(publicKey));
        var now = DateTimeOffset.UtcNow;
        var status = Sign(new SignedLicenseStatusDocument
        {
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddHours(1),
            Licenses = [new LicenseStatusEntry("license-123", false)]
        }, key);

        Assert.That(validator.IsStatusValid(status, "license-123", now), Is.True);
        Assert.That(validator.IsStatusValid(Sign(status with { Licenses = [new LicenseStatusEntry("license-123", true)] }, key), "license-123", now), Is.False);
        Assert.That(validator.IsStatusValid(Sign(status with { ExpiresAt = now.AddSeconds(-1) }, key), "license-123", now), Is.False);
        Assert.That(validator.IsStatusValid(status with { Licenses = [new LicenseStatusEntry("license-123", true)] }, "license-123", now), Is.False);
        Assert.That(validator.IsStatusValid(Sign(status with { Licenses = [.. status.Licenses, new LicenseStatusEntry("license-123", false)] }, key), "license-123", now), Is.False);
    }

    [Test]
    public void IsRunTokenValid_BindsTokenToLicenseNonceAndShortLifetime()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var publicKey = ECDsa.Create(key.ExportParameters(false));
        var validator = new LicenseValidator(new LicenseVerifier(publicKey));
        var now = DateTimeOffset.UtcNow;
        var token = Sign(new RunToken
        {
            LicenseId = "license-123",
            RunNonce = "nonce-456",
            IssuedAt = now.AddSeconds(-10),
            ExpiresAt = now.AddMinutes(2)
        }, key);

        Assert.That(validator.IsRunTokenValid(token, "license-123", "nonce-456", now, TimeSpan.FromMinutes(5)), Is.True);
        Assert.That(validator.IsRunTokenValid(token, "license-123", "other", now, TimeSpan.FromMinutes(5)), Is.False);
        Assert.That(validator.IsRunTokenValid(token, "license-123", "nonce-456", now, TimeSpan.FromSeconds(30)), Is.False);
        Assert.That(validator.IsRunTokenValid(Sign(token with { ExpiresAt = now.AddSeconds(-1) }, key), "license-123", "nonce-456", now, TimeSpan.FromMinutes(5)), Is.False);
    }

    [Test]
    public void IsOutputReceiptValid_RequiresExactOutputHashAndSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var publicKey = ECDsa.Create(key.ExportParameters(false));
        var validator = new LicenseValidator(new LicenseVerifier(publicKey));
        var now = DateTimeOffset.UtcNow;
        var request = new OutputSignatureRequest("license-123", "nonce-456", new string('a', 64));
        var receipt = Sign(new SignedOutputReceipt
        {
            LicenseId = request.LicenseId,
            CustomerId = "customer-1",
            RunNonce = request.RunNonce,
            OutputSha256 = request.OutputSha256,
            IssuedAt = now
        }, key);

        Assert.That(validator.IsOutputReceiptValid(receipt, request, "customer-1", now), Is.True);
        Assert.That(validator.IsOutputReceiptValid(receipt, request with { OutputSha256 = new string('b', 64) }, "customer-1", now), Is.False);
    }

    private static LicenseDocument Sign(LicenseDocument document, ECDsa key) =>
        document with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(document), key) };

    private static SignedLicenseStatusDocument Sign(SignedLicenseStatusDocument document, ECDsa key) =>
        document with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(document), key) };

    private static RunToken Sign(RunToken token, ECDsa key) =>
        token with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(token), key) };

    private static SignedOutputReceipt Sign(SignedOutputReceipt receipt, ECDsa key) =>
        receipt with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(receipt), key) };
}
