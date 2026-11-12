using System.Text.Json;
using SharpShift.Inventory.Core.Licensing;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Core.Utilities;

public static class InventoryOutputReader
{
    public static List<SolutionInventory> Deserialize(string json, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<List<SolutionInventory>>(json, options) ?? [];

        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(nameof(SignedInventoryDocument.Inventory), out _))
        {
            var signedDocument = JsonSerializer.Deserialize<SignedInventoryDocument>(json, options)
                ?? throw new JsonException("Signed inventory document is empty.");
            return signedDocument.Inventory;
        }

        throw new JsonException("Inventory JSON must be an array or a signed inventory document.");
    }
}
