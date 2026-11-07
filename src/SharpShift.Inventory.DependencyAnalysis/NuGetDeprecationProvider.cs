using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using SharpShift.LoggerMessages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SharpShift.Inventory.DependencyAnalysis;

public sealed class NuGetDeprecationProvider : IDependencyDeprecationProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly HttpRequestExecutor _requestExecutor;
    private readonly DependencyHealthCheckOptions _options;
    private readonly ConcurrentDictionary<string, string> _registrationBaseUrlCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<PackageHealthQuery, Task<IReadOnlyList<PackageDeprecationRecord>>> _queryCache = new();

    public NuGetDeprecationProvider(HttpClient httpClient, DependencyHealthCheckOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new DependencyHealthCheckOptions();
        _requestExecutor = new HttpRequestExecutor(_httpClient, _options);
    }

    public Task<IReadOnlyList<PackageDeprecationRecord>> GetDeprecationsAsync(Microsoft.Extensions.Logging.ILogger logger, PackageHealthQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _queryCache.GetOrAdd(query, q => GetDeprecationsCoreAsync(logger, q, cancellationToken));
    }

    private async Task<IReadOnlyList<PackageDeprecationRecord>> GetDeprecationsCoreAsync(
        Microsoft.Extensions.Logging.ILogger logger,
        PackageHealthQuery query,
        CancellationToken cancellationToken)
    {
        LogMessages.AnalyzingDependency(logger, query.PackageId, query.Version);

        var registrationBaseUrl = await ResolveRegistrationBaseUrlAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(registrationBaseUrl))
            return [];

        var registrationIndexUrl = $"{registrationBaseUrl.TrimEnd('/')}/{query.PackageId.ToLowerInvariant()}/index.json";

        using var response = await _requestExecutor.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, registrationIndexUrl),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var registration = await JsonSerializer.DeserializeAsync<RegistrationIndexResponse>(stream, JsonOptions, cancellationToken: cancellationToken);

        if (registration?.Items is null || registration.Items.Count == 0)
            return [];

        var records = registration.Items
            .SelectMany(page => page.Items ?? [])
            .Select(leaf => leaf.CatalogEntry)
            .Where(entry =>
                entry is not null
                && string.Equals(entry.Version, query.Version, StringComparison.OrdinalIgnoreCase)
                && entry.Deprecation is not null)
            .Select(entry => new PackageDeprecationRecord(
                query.PackageId,
                query.Version,
                entry!.Deprecation!.Message ?? string.Empty,
                entry.Deprecation.AlternatePackage?.Id,
                null))
            .ToList();

        return records;
    }

    private async Task<string> ResolveRegistrationBaseUrlAsync(CancellationToken cancellationToken)
    {
        if (_registrationBaseUrlCache.TryGetValue(_options.NuGetServiceIndexUrl, out var cachedUrl))
            return cachedUrl;

        using var response = await _requestExecutor.SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, _options.NuGetServiceIndexUrl), cancellationToken);
        if (!response.IsSuccessStatusCode)
            return string.Empty;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var index = await JsonSerializer.DeserializeAsync<ServiceIndexResponse>(stream, JsonOptions, cancellationToken: cancellationToken);
        var registration = index?.Resources?
            .FirstOrDefault(r => r.Type?.Contains("RegistrationsBaseUrl", StringComparison.OrdinalIgnoreCase) == true);

        var registrationBaseUrl = registration?.Id ?? string.Empty;
        _registrationBaseUrlCache.TryAdd(_options.NuGetServiceIndexUrl, registrationBaseUrl);
        return registrationBaseUrl;
    }

    private sealed class ServiceIndexResponse
    {
        public List<ServiceResource>? Resources { get; set; }
    }

    private sealed class ServiceResource
    {
        [JsonPropertyName("@id")]
        public string? Id { get; set; }

        [JsonPropertyName("@type")]
        public string? Type { get; set; }
    }

    private sealed class RegistrationIndexResponse
    {
        public List<RegistrationPage>? Items { get; set; }
    }

    private sealed class RegistrationPage
    {
        public List<RegistrationLeaf>? Items { get; set; }
    }

    private sealed class RegistrationLeaf
    {
        public RegistrationCatalogEntry? CatalogEntry { get; set; }
    }

    private sealed class RegistrationCatalogEntry
    {
        public string? Version { get; set; }
        public DeprecationMetadata? Deprecation { get; set; }
    }

    private sealed class DeprecationMetadata
    {
        public string? Message { get; set; }
        public AlternatePackageMetadata? AlternatePackage { get; set; }
    }

    private sealed class AlternatePackageMetadata
    {
        public string? Id { get; set; }
        public string? Range { get; set; }
    }
}

internal static class TaskResultExtensions
{
    public static Task<T> AsTaskResult<T>(this T value) => Task.FromResult(value);
}
