using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.DependencyAnalysis;
using SharpShift.LoggerMessages;
using System.Net;
using System.Net.Http;
using System.Text;

namespace SharpShift.Inventory.Tests;

public class DependencyHealthAnalyzerTests
{
    [Test]
    public void SeverityMapper_MapsCvssAndText()
    {
        Assert.That(DependencySeverityMapper.FromCvss(9.8), Is.EqualTo(RiskSeverity.Critical));
        Assert.That(DependencySeverityMapper.FromCvss(7.2), Is.EqualTo(RiskSeverity.High));
        Assert.That(DependencySeverityMapper.FromCvss(5.4), Is.EqualTo(RiskSeverity.Medium));
        Assert.That(DependencySeverityMapper.FromCvss(2.1), Is.EqualTo(RiskSeverity.Low));
        Assert.That(DependencySeverityMapper.FromString("HIGH"), Is.EqualTo(RiskSeverity.High));
    }

    [Test]
    public async Task NuGetDeprecationProvider_ParsesDeprecation()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Equals("https://api.nuget.org/v3/index.json", StringComparison.OrdinalIgnoreCase)
                && request.Method == HttpMethod.Get)
            {
                return Json("""
                {
                  "resources": [
                    { "@id": "https://api.nuget.org/v3/registration5-semver1/", "@type": "RegistrationsBaseUrl/3.6.0" }
                  ]
                }
                """);
            }

            if (request.RequestUri.AbsoluteUri.Contains("registration5-semver1/test.package/index.json", StringComparison.OrdinalIgnoreCase))
            {
                return Json("""
                {
                  "items": [
                    {
                      "items": [
                        {
                          "catalogEntry": {
                            "version": "1.0.0",
                            "deprecation": {
                              "message": "Package is deprecated",
                              "alternatePackage": { "id": "test.package.new", "range": "2.0.0" }
                            }
                          }
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var provider = new NuGetDeprecationProvider(new HttpClient(handler));
        var results = await provider.GetDeprecationsAsync(NullLogger.Instance, new PackageHealthQuery("Test.Package", "1.0.0"), CancellationToken.None);

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].AlternatePackageId, Is.EqualTo("test.package.new"));
    }

    [Test]
    public async Task OsvVulnerabilityProvider_ParsesVulnerability()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""
        {
          "vulns": [
            {
              "id": "GHSA-1234",
              "summary": "Example issue",
              "details": "More details",
              "severity": [ { "score": "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H/9.8" } ],
              "references": [ { "url": "https://example.test/advisory" } ]
            }
          ]
        }
        """));

        var provider = new OsvVulnerabilityProvider(new HttpClient(handler));
        var results = await provider.GetVulnerabilitiesAsync(new PackageHealthQuery("Pkg", "1.2.3"), CancellationToken.None);

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Severity, Is.EqualTo(RiskSeverity.Critical));
        Assert.That(results[0].AdvisoryId, Is.EqualTo("GHSA-1234"));
    }

    [Test]
    public async Task DependencyHealthAnalyzer_DeduplicatesAndContinuesOnProviderFailure()
    {
        var result = new ProjectAnalysisResult
        {
            Dependencies = new Dictionary<string, DependencySummary>
            {
                ["Pkg"] = new DependencySummary
                {
                    Name = "Pkg",
                    DependencyType = "Package",
                    Version = new Dictionary<string, int>
                    {
                        ["1.0.0"] = 2
                    }
                }
            }
        };

        var analyzer = new DependencyHealthAnalyzer(
            deprecationProvider: new StaticDeprecationProvider(new[]
            {
                new PackageDeprecationRecord("Pkg", "1.0.0", "Deprecated", null, null),
                new PackageDeprecationRecord("Pkg", "1.0.0", "Deprecated", null, null)
            }),
            vulnerabilityProvider: new ThrowingVulnerabilityProvider());

        await analyzer.AnalyzeAsync(NullLogger.Instance, result);

        Assert.That(result.RiskFindings.Count, Is.EqualTo(1));
        Assert.That(result.RiskFindings[0].Category, Is.EqualTo("Dependency.Deprecated"));
    }

    private static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }

    private sealed class StaticDeprecationProvider(IEnumerable<PackageDeprecationRecord> records) : IDependencyDeprecationProvider
    {
        public Task<IReadOnlyList<PackageDeprecationRecord>> GetDeprecationsAsync(ILogger logger, PackageHealthQuery query, CancellationToken cancellationToken)
        {
            LogMessages.AnalyzingDependency(logger, query.PackageId, query.Version);
            return Task.FromResult<IReadOnlyList<PackageDeprecationRecord>>([..records]);
        }
    }

    private sealed class ThrowingVulnerabilityProvider : IDependencyVulnerabilityProvider
    {
        public Task<IReadOnlyList<PackageVulnerabilityRecord>> GetVulnerabilitiesAsync(PackageHealthQuery query, CancellationToken cancellationToken)
            => throw new HttpRequestException("simulated failure");
    }
}
