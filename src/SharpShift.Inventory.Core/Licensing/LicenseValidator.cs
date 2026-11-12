using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SharpShift.Inventory.Core.Licensing;

public sealed class LicenseValidator(LicenseVerifier verifier)
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);
    private readonly LicenseVerifier _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));

    public bool IsLicenseValid(LicenseDocument license, DateTimeOffset now, string requiredScope)
    {
        ArgumentNullException.ThrowIfNull(license);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredScope);

        return !string.IsNullOrWhiteSpace(license.LicenseId)
            && !string.IsNullOrWhiteSpace(license.CustomerId)
            && !string.IsNullOrWhiteSpace(license.ContractorId)
            && license.ExpiresAt > license.IssuedAt
            && license.IssuedAt <= now + ClockSkew
            && license.ExpiresAt > now
            && license.Scopes is not null
            && license.Scopes.Contains(requiredScope, StringComparer.Ordinal)
            && _verifier.VerifyLicenseSignature(license);
    }

    public bool IsStatusValid(SignedLicenseStatusDocument status, string licenseId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseId);

        if (status.ExpiresAt <= status.IssuedAt
            || status.IssuedAt > now + ClockSkew
            || status.ExpiresAt <= now
            || status.Licenses is null
            || status.Licenses.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.LicenseId))
            || !_verifier.VerifyStatusSignature(status))
        {
            return false;
        }

        var matchingEntries = status.Licenses.Where(entry => string.Equals(entry.LicenseId, licenseId, StringComparison.Ordinal)).ToArray();
        return matchingEntries.Length == 1 && !matchingEntries[0].Revoked;
    }

    public bool IsRunTokenValid(RunToken token, string licenseId, string runNonce, DateTimeOffset now, TimeSpan maximumLifetime)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runNonce);
        if (maximumLifetime <= TimeSpan.Zero)
            return false;

        return string.Equals(token.LicenseId, licenseId, StringComparison.Ordinal)
            && string.Equals(token.RunNonce, runNonce, StringComparison.Ordinal)
            && token.ExpiresAt > token.IssuedAt
            && token.IssuedAt <= now + ClockSkew
            && token.ExpiresAt > now
            && token.ExpiresAt - token.IssuedAt <= maximumLifetime
            && _verifier.VerifyRunTokenSignature(token);
    }

    public bool IsOutputReceiptValid(SignedOutputReceipt receipt, OutputSignatureRequest request, string customerId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        if (string.IsNullOrWhiteSpace(receipt.OutputSha256)
            || string.IsNullOrWhiteSpace(request.OutputSha256)
            || string.IsNullOrWhiteSpace(receipt.Signature))
        {
            return false;
        }

        return string.Equals(receipt.LicenseId, request.LicenseId, StringComparison.Ordinal)
            && string.Equals(receipt.CustomerId, customerId, StringComparison.Ordinal)
            && string.Equals(receipt.RunNonce, request.RunNonce, StringComparison.Ordinal)
            && string.Equals(receipt.OutputSha256, request.OutputSha256, StringComparison.OrdinalIgnoreCase)
            && Regex.IsMatch(receipt.OutputSha256, "\\A[0-9a-fA-F]{64}\\z", RegexOptions.CultureInvariant)
            && receipt.IssuedAt <= now + ClockSkew
            && _verifier.VerifyOutputReceiptSignature(receipt);
    }
}
