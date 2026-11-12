using System.Security.Cryptography;
using System.Text.Json;
using SharpShift.Inventory.Core.Licensing;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

try
{
    return args[0] switch
    {
        "license" => await IssueLicenseAsync(args, jsonOptions),
        "status" => await GenerateStatusAsync(args, jsonOptions),
        _ => PrintUsageAndFail()
    };
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException or FormatException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static async Task<int> IssueLicenseAsync(string[] args, JsonSerializerOptions jsonOptions)
{
    if (args.Length != 6 || !DateTimeOffset.TryParse(args[4], out var expiresAt))
        return PrintUsageAndFail();

    using var signingKey = LoadSigningKey(args[1]);
    var license = new LicenseDocument
    {
        LicenseId = Guid.NewGuid().ToString("N"),
        CustomerId = args[2],
        ContractorId = args[3],
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = expiresAt.ToUniversalTime(),
        Scopes = ["inventory"]
    };
    if (string.IsNullOrWhiteSpace(license.CustomerId) || string.IsNullOrWhiteSpace(license.ContractorId) || license.ExpiresAt <= license.IssuedAt)
        throw new ArgumentException("Customer, contractor, and a future expiry are required.");

    license = license with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(license), signingKey) };
    await File.WriteAllTextAsync(args[5], JsonSerializer.Serialize(license, jsonOptions));
    return 0;
}

static async Task<int> GenerateStatusAsync(string[] args, JsonSerializerOptions jsonOptions)
{
    if (args.Length < 5)
        return PrintUsageAndFail();

    using var signingKey = LoadSigningKey(args[1]);
    await using var inputStream = File.OpenRead(args[2]);
    var entries = await JsonSerializer.DeserializeAsync<List<LicenseStatusEntry>>(inputStream, jsonOptions)
        ?? throw new JsonException("Status input must be a JSON array.");
    if (entries.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.LicenseId))
        || entries.Select(entry => entry.LicenseId).Distinct(StringComparer.Ordinal).Count() != entries.Count)
    {
        throw new ArgumentException("Status entries must have unique, non-empty license IDs.");
    }

    var now = DateTimeOffset.UtcNow;
    var status = new SignedLicenseStatusDocument
    {
        IssuedAt = now,
        ExpiresAt = now.AddHours(1),
        Licenses = entries
    };
    status = status with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(status), signingKey) };
    var json = JsonSerializer.Serialize(status, jsonOptions);
    foreach (var outputPath in args.Skip(3))
        await File.WriteAllTextAsync(outputPath, json);
    return 0;
}

static ECDsa LoadSigningKey(string path)
{
    var key = ECDsa.Create();
    try
    {
        key.ImportFromPem(File.ReadAllText(path));
        if (key.KeySize != 256)
            throw new CryptographicException("The signing key must use ECDSA P-256.");
        return key;
    }
    catch
    {
        key.Dispose();
        throw;
    }
}

static int PrintUsageAndFail()
{
    PrintUsage();
    return 2;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  license <private-key.pem> <customer-id> <contractor-id> <expires-at-ISO-8601> <output.json>");
    Console.Error.WriteLine("  status <private-key.pem> <entries.json> <output-origin-1> <output-origin-2> [additional-origins ...]");
}
