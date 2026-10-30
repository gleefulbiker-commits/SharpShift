namespace SharpShift.Inventory.DependencyAnalysis;

public sealed record PackageDeprecationRecord(
    string PackageId,
    string Version,
    string Message,
    string? AlternatePackageId,
    string? Url);
