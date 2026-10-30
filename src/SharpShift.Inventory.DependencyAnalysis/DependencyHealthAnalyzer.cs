using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SharpShift.Inventory.DependencyAnalysis;

public sealed class DependencyHealthAnalyzer
{
    private readonly IDependencyDeprecationProvider _deprecationProvider;
    private readonly IDependencyVulnerabilityProvider _vulnerabilityProvider;
    private readonly DependencyHealthCheckOptions _options;

    public DependencyHealthAnalyzer(
        IDependencyDeprecationProvider? deprecationProvider = null,
        IDependencyVulnerabilityProvider? vulnerabilityProvider = null,
        DependencyHealthCheckOptions? options = null,
        HttpClient? httpClient = null)
    {
        _options = options ?? new DependencyHealthCheckOptions();
        var sharedHttpClient = httpClient ?? new HttpClient();

        _deprecationProvider = deprecationProvider ?? new NuGetDeprecationProvider(sharedHttpClient, _options);
        _vulnerabilityProvider = vulnerabilityProvider ?? new OsvVulnerabilityProvider(sharedHttpClient, _options);
    }

    public async Task AnalyzeAsync(ILogger logger, ProjectAnalysisResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(result);

        if (!_options.EnableChecks)
        {
            logger.LogInformation("Dependency health checks are disabled.");
            return;
        }

        var packageQueries = result.Dependencies.Values
            .Where(d => string.Equals(d.DependencyType, "Package", StringComparison.OrdinalIgnoreCase))
            .SelectMany(d => d.Version?.Keys
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => new PackageHealthQuery(d.Name, v))
                ?? [])
            .Distinct()
            .ToList();

        if (packageQueries.Count == 0)
        {
            logger.LogInformation("No package dependencies with versions were found for dependency health checks.");
            return;
        }

        var uniqueRiskKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in packageQueries)
        {
            try
            {
                var deprecations = await _deprecationProvider.GetDeprecationsAsync(logger, query, cancellationToken);
                foreach (var deprecation in deprecations)
                {
                    var key = $"deprecation|{deprecation.PackageId}|{deprecation.Version}|{deprecation.Message}";
                    if (!uniqueRiskKeys.Add(key))
                        continue;

                    var alternateInfo = string.IsNullOrWhiteSpace(deprecation.AlternatePackageId)
                        ? string.Empty
                        : $" Alternate package: {deprecation.AlternatePackageId}.";
                    var evidence = string.IsNullOrWhiteSpace(deprecation.Url)
                        ? query.Version
                        : $"{query.Version} | {deprecation.Url}";

                    result.RiskFindings.Add(new RiskFinding
                    {
                        Category = "Dependency.Deprecated",
                        Severity = RiskSeverity.Medium,
                        Description = $"{deprecation.PackageId} {deprecation.Version} is deprecated. {deprecation.Message}.{alternateInfo}".Trim(),
                        Evidence = evidence
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Deprecation check failed for package {PackageId} {Version}", query.PackageId, query.Version);
            }

            try
            {
                var vulnerabilities = await _vulnerabilityProvider.GetVulnerabilitiesAsync(query, cancellationToken);
                foreach (var vulnerability in vulnerabilities)
                {
                    var key = $"vuln|{vulnerability.PackageId}|{vulnerability.Version}|{vulnerability.AdvisoryId}";
                    if (!uniqueRiskKeys.Add(key))
                        continue;

                    var description = string.IsNullOrWhiteSpace(vulnerability.Details)
                        ? vulnerability.Summary
                        : $"{vulnerability.Summary} {vulnerability.Details}";

                    var evidence = string.IsNullOrWhiteSpace(vulnerability.Url)
                        ? vulnerability.AdvisoryId
                        : $"{vulnerability.AdvisoryId} | {vulnerability.Url}";

                    result.RiskFindings.Add(new RiskFinding
                    {
                        Category = "Dependency.Vulnerability",
                        Severity = vulnerability.Severity,
                        Description = $"{vulnerability.PackageId} {vulnerability.Version}: {description}",
                        Evidence = evidence
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Vulnerability check failed for package {PackageId} {Version}", query.PackageId, query.Version);
            }
        }
    }
}
