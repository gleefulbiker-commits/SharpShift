using System.Security.Cryptography;
using SharpShift.Inventory.Core.Licensing;

namespace SharpShift.Inventory.Tests;

internal sealed class TestLicenseAuthorizationClient : ILicenseAuthorizationClient
{
    public Task<AuthorizedRun> AuthorizeRunAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthorizedRun(
            new LicenseDocument { LicenseId = "test-license", CustomerId = "test-customer" },
            new RunToken { LicenseId = "test-license", RunNonce = "test-nonce" }));

    public Task<SignedOutputReceipt> SignOutputAsync(
        AuthorizedRun run,
        ReadOnlyMemory<byte> inventoryBytes,
        CancellationToken cancellationToken = default)
    {
        var receipt = new SignedOutputReceipt
        {
            LicenseId = run.License.LicenseId,
            CustomerId = run.License.CustomerId,
            RunNonce = run.Token.RunNonce,
            OutputSha256 = Convert.ToHexString(SHA256.HashData(inventoryBytes.Span)).ToLowerInvariant(),
            IssuedAt = DateTimeOffset.UtcNow,
            Signature = "test-signature"
        };
        return Task.FromResult(receipt);
    }
}
