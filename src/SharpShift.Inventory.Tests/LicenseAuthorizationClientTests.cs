using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;
using SharpShift.Inventory.Core.Licensing;

namespace SharpShift.Inventory.Tests;

[TestFixture]
public sealed class LicenseAuthorizationClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task AuthorizeAndSignOutput_UsesFallbackStatusAndReturnsVerifiableReceipt()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var license = Sign(new LicenseDocument
        {
            LicenseId = "license-123",
            CustomerId = "customer-1",
            ContractorId = "contractor-1",
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddHours(1),
            Scopes = ["inventory"]
        }, signingKey);
        var status = Sign(new SignedLicenseStatusDocument
        {
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddMinutes(59),
            Licenses = [new LicenseStatusEntry(license.LicenseId, false)]
        }, signingKey);
        var observed = new List<string>();
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            observed.Add(request.RequestUri!.Host);
            if (request.RequestUri.Host == "primary.test")
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            if (request.RequestUri.AbsolutePath.StartsWith("/status/", StringComparison.Ordinal))
                return Json(status);
            if (request.RequestUri.AbsolutePath == "/run-tokens")
            {
                var tokenRequest = await request.Content!.ReadFromJsonAsync<RunTokenRequest>(JsonOptions, cancellationToken);
                var token = Sign(new RunToken
                {
                    LicenseId = tokenRequest!.LicenseId,
                    RunNonce = tokenRequest.RunNonce,
                    IssuedAt = now,
                    ExpiresAt = now.AddMinutes(4)
                }, signingKey);
                return Json(token);
            }
            if (request.RequestUri.AbsolutePath == "/output-signatures")
            {
                var outputRequest = await request.Content!.ReadFromJsonAsync<OutputSignatureRequest>(JsonOptions, cancellationToken);
                var receipt = Sign(new SignedOutputReceipt
                {
                    LicenseId = outputRequest!.LicenseId,
                    CustomerId = license.CustomerId,
                    RunNonce = outputRequest.RunNonce,
                    OutputSha256 = outputRequest.OutputSha256,
                    IssuedAt = now
                }, signingKey);
                return Json(receipt);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var path = await WriteLicenseAsync(license);
        try
        {
            using var verifier = new LicenseVerifier(ECDsa.Create(signingKey.ExportParameters(false)));
            using var client = CreateClient(path, verifier, handler);
            var run = await client.AuthorizeRunAsync(now);
            var inventory = JsonSerializer.SerializeToUtf8Bytes(new[] { new { solution = "sample" } });
            var receipt = await client.SignOutputAsync(run, inventory);
            var request = new OutputSignatureRequest(
                run.License.LicenseId,
                run.Token.RunNonce,
                Convert.ToHexString(SHA256.HashData(inventory)).ToLowerInvariant());

            Assert.That(observed.Take(2), Is.EqualTo(new[] { "primary.test", "secondary.test" }));
            Assert.That(new LicenseValidator(verifier).IsOutputReceiptValid(receipt, request, license.CustomerId, DateTimeOffset.UtcNow), Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AuthorizeRunAsync_RejectsMissingInvalidAndExpiredLicenses()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var verifier = new LicenseVerifier(ECDsa.Create(signingKey.ExportParameters(false)));
        var now = DateTimeOffset.UtcNow;
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Sign(new SignedLicenseStatusDocument
            {
                IssuedAt = now.AddMinutes(-1),
                ExpiresAt = now.AddMinutes(30),
                Licenses = [new LicenseStatusEntry("license-123", false)]
            }, signingKey), options: JsonOptions)
        }));

        using var missing = CreateClient(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), verifier, handler);
        await Assert.ThrowsAsync<LicenseAuthorizationException>(() => missing.AuthorizeRunAsync(now));

        var valid = Sign(new LicenseDocument
        {
            LicenseId = "license-123",
            CustomerId = "customer-1",
            ContractorId = "contractor-1",
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddHours(1),
            Scopes = ["inventory"]
        }, signingKey);
        var invalidPath = await WriteLicenseAsync(valid with { CustomerId = "tampered" });
        try
        {
            using var invalid = CreateClient(invalidPath, verifier, handler);
            await Assert.ThrowsAsync<LicenseAuthorizationException>(() => invalid.AuthorizeRunAsync(now));
        }
        finally
        {
            File.Delete(invalidPath);
        }

        var expiredPath = await WriteLicenseAsync(Sign(valid with { ExpiresAt = now.AddSeconds(-1) }, signingKey));
        try
        {
            using var expired = CreateClient(expiredPath, verifier, handler);
            await Assert.ThrowsAsync<LicenseAuthorizationException>(() => expired.AuthorizeRunAsync(now));
        }
        finally
        {
            File.Delete(expiredPath);
        }
    }

    [Test]
    public async Task AuthorizeRunAsync_FailsClosedWhenEveryStatusEndpointIsUnavailable()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var verifier = new LicenseVerifier(ECDsa.Create(signingKey.ExportParameters(false)));
        var now = DateTimeOffset.UtcNow;
        var license = Sign(new LicenseDocument
        {
            LicenseId = "license-123",
            CustomerId = "customer-1",
            ContractorId = "contractor-1",
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddHours(1),
            Scopes = ["inventory"]
        }, signingKey);
        var path = await WriteLicenseAsync(license);
        var calls = 0;
        var handler = new StubHandler((_, _) =>
        {
            calls++;
            throw new HttpRequestException("endpoint unavailable");
        });

        try
        {
            using var client = CreateClient(path, verifier, handler);
            var failure = await Assert.ThrowsAsync<LicenseAuthorizationException>(() => client.AuthorizeRunAsync(now));
            Assert.That(failure!.Message, Is.EqualTo("No configured status endpoint returned a valid active status for this license."));
            Assert.That(calls, Is.EqualTo(3));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AuthorizeRunAsync_RejectsExpiredRunToken()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var verifier = new LicenseVerifier(ECDsa.Create(signingKey.ExportParameters(false)));
        var now = DateTimeOffset.UtcNow;
        var license = Sign(new LicenseDocument
        {
            LicenseId = "license-123",
            CustomerId = "customer-1",
            ContractorId = "contractor-1",
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddHours(1),
            Scopes = ["inventory"]
        }, signingKey);
        var status = Sign(new SignedLicenseStatusDocument
        {
            IssuedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddMinutes(30),
            Licenses = [new LicenseStatusEntry(license.LicenseId, false)]
        }, signingKey);
        var path = await WriteLicenseAsync(license);
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath.StartsWith("/status/", StringComparison.Ordinal))
                return Json(status);
            if (request.RequestUri.AbsolutePath == "/run-tokens")
            {
                var tokenRequest = await request.Content!.ReadFromJsonAsync<RunTokenRequest>(JsonOptions, cancellationToken);
                return Json(Sign(new RunToken
                {
                    LicenseId = tokenRequest!.LicenseId,
                    RunNonce = tokenRequest.RunNonce,
                    IssuedAt = now.AddMinutes(-2),
                    ExpiresAt = now.AddSeconds(-1)
                }, signingKey));
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        try
        {
            using var client = CreateClient(path, verifier, handler);
            await Assert.ThrowsAsync<LicenseAuthorizationException>(() => client.AuthorizeRunAsync(now));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static LicenseAuthorizationClient CreateClient(string licensePath, LicenseVerifier verifier, HttpMessageHandler handler) =>
        new(new LicenseAuthorizationOptions(
            new Uri("https://primary.test/"),
            new Uri("https://secondary.test/"),
            new Uri("https://verify.test/"),
            licensePath,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMinutes(5)), verifier, new HttpClient(handler));

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value, options: JsonOptions)
    };

    private static async Task<string> WriteLicenseAsync(LicenseDocument license)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(license, JsonOptions));
        return path;
    }

    private static LicenseDocument Sign(LicenseDocument license, ECDsa key) =>
        license with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(license), key) };

    private static SignedLicenseStatusDocument Sign(SignedLicenseStatusDocument status, ECDsa key) =>
        status with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(status), key) };

    private static RunToken Sign(RunToken token, ECDsa key) =>
        token with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(token), key) };

    private static SignedOutputReceipt Sign(SignedOutputReceipt receipt, ECDsa key) =>
        receipt with { Signature = LicenseSignature.Sign(LicenseSignature.GetPayload(receipt), key) };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
