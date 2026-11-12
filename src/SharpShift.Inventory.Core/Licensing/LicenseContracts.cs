using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace SharpShift.Inventory.Core.Licensing;

public sealed record LicenseDocument
{
    public string LicenseId { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string ContractorId { get; init; } = string.Empty;
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = [];
    public string Signature { get; init; } = string.Empty;
}

public sealed record LicenseStatusEntry(string LicenseId, bool Revoked);

public sealed record SignedLicenseStatusDocument
{
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public IReadOnlyList<LicenseStatusEntry> Licenses { get; init; } = [];
    public string Signature { get; init; } = string.Empty;
}

public sealed record RunToken
{
    public string LicenseId { get; init; } = string.Empty;
    public string RunNonce { get; init; } = string.Empty;
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string Signature { get; init; } = string.Empty;
}

public sealed record OutputSignatureRequest(string LicenseId, string RunNonce, string OutputSha256);

public sealed record SignedOutputReceipt
{
    public string LicenseId { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string RunNonce { get; init; } = string.Empty;
    public string OutputSha256 { get; init; } = string.Empty;
    public DateTimeOffset IssuedAt { get; init; }
    public string Signature { get; init; } = string.Empty;
}

public enum LicenseStatus
{
    Active,
    Revoked
}

public static class LicenseSignature
{
    public static byte[] GetPayload(LicenseDocument license) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("licenseId", license.LicenseId);
        writer.WriteString("customerId", license.CustomerId);
        writer.WriteString("contractorId", license.ContractorId);
        writer.WriteString("issuedAt", FormatDate(license.IssuedAt));
        writer.WriteString("expiresAt", FormatDate(license.ExpiresAt));
        writer.WritePropertyName("scopes");
        writer.WriteStartArray();
        foreach (var scope in license.Scopes.Order(StringComparer.Ordinal))
            writer.WriteStringValue(scope);
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    public static byte[] GetPayload(SignedLicenseStatusDocument status) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("issuedAt", FormatDate(status.IssuedAt));
        writer.WriteString("expiresAt", FormatDate(status.ExpiresAt));
        writer.WritePropertyName("licenses");
        writer.WriteStartArray();
        foreach (var entry in status.Licenses.OrderBy(item => item.LicenseId, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("licenseId", entry.LicenseId);
            writer.WriteBoolean("revoked", entry.Revoked);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    public static byte[] GetPayload(RunToken token) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("licenseId", token.LicenseId);
        writer.WriteString("runNonce", token.RunNonce);
        writer.WriteString("issuedAt", FormatDate(token.IssuedAt));
        writer.WriteString("expiresAt", FormatDate(token.ExpiresAt));
        writer.WriteEndObject();
    });

    public static byte[] GetPayload(SignedOutputReceipt receipt) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("licenseId", receipt.LicenseId);
        writer.WriteString("customerId", receipt.CustomerId);
        writer.WriteString("runNonce", receipt.RunNonce);
        writer.WriteString("outputSha256", receipt.OutputSha256);
        writer.WriteString("issuedAt", FormatDate(receipt.IssuedAt));
        writer.WriteEndObject();
    });

    public static string Sign(ReadOnlySpan<byte> payload, ECDsa privateKey) =>
        Convert.ToBase64String(privateKey.SignData(payload, HashAlgorithmName.SHA256));

    public static bool Verify(ReadOnlySpan<byte> payload, string signature, ECDsa publicKey)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;

        try
        {
            return publicKey.VerifyData(payload, Convert.FromBase64String(signature), HashAlgorithmName.SHA256);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static byte[] Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            write(writer);
        return stream.ToArray();
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}

public sealed class LicenseVerifier(ECDsa publicKey) : IDisposable
{
    private readonly ECDsa _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));

    public bool VerifyLicenseSignature(LicenseDocument license) =>
        LicenseSignature.Verify(LicenseSignature.GetPayload(license), license.Signature, _publicKey);

    public bool VerifyStatusSignature(SignedLicenseStatusDocument status) =>
        LicenseSignature.Verify(LicenseSignature.GetPayload(status), status.Signature, _publicKey);

    public bool VerifyRunTokenSignature(RunToken token) =>
        LicenseSignature.Verify(LicenseSignature.GetPayload(token), token.Signature, _publicKey);

    public bool VerifyOutputReceiptSignature(SignedOutputReceipt receipt) =>
        LicenseSignature.Verify(LicenseSignature.GetPayload(receipt), receipt.Signature, _publicKey);

    public static ECDsa? LoadEmbeddedPublicKey()
    {
        using var stream = typeof(LicenseVerifier).Assembly.GetManifestResourceStream("SharpShift.Inventory.Core.Licensing.license-public-key.pem")
            ?? throw new InvalidOperationException("The embedded licensing public key resource is missing.");
        using var reader = new StreamReader(stream);
        var pem = reader.ReadToEnd();
        return pem.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)
            ? LoadPublicKey(pem)
            : null;
    }

    public static ECDsa LoadPublicKey(string subjectPublicKeyInfoPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectPublicKeyInfoPem);
        var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(subjectPublicKeyInfoPem);
            if (key.KeySize != 256)
                throw new CryptographicException("The licensing public key must use ECDSA P-256.");
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public void Dispose() => _publicKey.Dispose();
}
