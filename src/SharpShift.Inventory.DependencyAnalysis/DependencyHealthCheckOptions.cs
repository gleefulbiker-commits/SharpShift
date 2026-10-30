using System;

namespace SharpShift.Inventory.DependencyAnalysis;

public sealed class DependencyHealthCheckOptions
{
    public bool EnableChecks { get; init; } = true;

    public string NuGetServiceIndexUrl { get; init; } = "https://api.nuget.org/v3/index.json";

    public string OsvQueryUrl { get; init; } = "https://api.osv.dev/v1/query";

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
