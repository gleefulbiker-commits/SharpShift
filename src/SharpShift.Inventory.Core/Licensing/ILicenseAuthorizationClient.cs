using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Core.Licensing;

public interface ILicenseAuthorizationClient
{
    Task<AuthorizedRun> AuthorizeRunAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<SignedOutputReceipt> SignOutputAsync(AuthorizedRun run, ReadOnlyMemory<byte> inventoryBytes, CancellationToken cancellationToken = default);
}

public sealed record SignedInventoryDocument(
    string CustomerId,
    string LicenseId,
    string RunNonce,
    List<SolutionInventory> Inventory,
    SignedOutputReceipt Receipt);
