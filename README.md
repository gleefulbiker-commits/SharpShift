# SharpShift Inventory

SharpShift Inventory — small CLI to discover solutions/projects, evaluate frameworks, and produce a JSON inventory.

Features
- Discover solutions and projects under a folder.
- Normalize target frameworks (SDK and legacy).
- Evaluate projects using MSBuild when available (reflection-based) with XML fallback.
- Extract dependencies (PackageReference and Reference) including version, hint path, public key token, and additional metadata.
- Analyze NuGet package dependencies for deprecations and known vulnerabilities.

Recent changes (this commit)
- Dependency metadata is now preserved as structured fields:
  - `HintPath` (string) — captured for assembly references.
  - `PublicKeyToken` (string) — parsed from reference include when present.
  - `OtherMetadata` (Dictionary<string,string>) — key/value pairs for remaining metadata (e.g., `PrivateAssets`).
  - `Metadata` (string) remains for backward compatibility (concatenated `key=value` pairs).
- `ProjectInventory` now includes a `Dependencies` list so dependency details are serialized into `inventory.json`.
- Added a best-effort unit test (`ProjectEvaluatorTests`) that asserts MSBuild-evaluated item metadata keys when MSBuild is available; the test skips in environments without item entries.

How to run
- `dotnet build src/SharpShift.slnx`
- `dotnet test src/SharpShift.slnx`
- Run `dotnet run --project src/SharpShift -- scan <rootPath> [options]` to create an inventory. Scan requires a valid online license configuration; see Licensing below.

## Licensing

Inventory scans fail closed: without a valid license, active signed status, and a fresh server-issued token, discovery does not start. Offline scans are not supported. License failures return exit code `4`; invalid paths/options return `2`; unexpected scan failures return `3`.

### Signing keys

Use an ECDSA P-256 private key controlled by the issuer. Never commit or distribute the private key. The CLI trust anchor is embedded at build time in `src/SharpShift.Inventory.Core/Licensing/license-public-key.pem`; replace that template with the issuer's public PEM before producing a release build. This pins the key in the client binary rather than trusting a replaceable runtime setting. The server and issuer must use the matching private key.

For example, create a key pair outside the repository with OpenSSL, then produce the public PEM:

```powershell
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out issuer-private.pem
openssl pkey -in issuer-private.pem -pubout -out license-public-key.pem
```

Keep `issuer-private.pem` in a managed key vault or protected issuer environment. Configure the service to read it from a mounted secret file; do not copy it into the CLI or source tree. Replace the embedded public PEM and rebuild/release the CLI whenever the signing key rotates.

### License, status, and token flow

A license JSON document contains `licenseId`, `customerId`, `contractorId`, `issuedAt`, `expiresAt`, `scopes`, and a Base64 ECDSA signature. The current CLI requires the `inventory` scope. The issuer creates licenses and signed status files:

```powershell
dotnet run --project src/SharpShift.Inventory.Licensing.Issuer -- license <private-key.pem> <customer-id> <contractor-id> <expires-at-ISO-8601> <license.json>
dotnet run --project src/SharpShift.Inventory.Licensing.Issuer -- status <private-key.pem> <entries.json> <origin-one\status.json> <origin-two\status.json>
```

The status input is a JSON array of `{ "licenseId": "...", "revoked": false }` entries. The tool signs a status document that expires after one hour and writes the same signed document to each origin path. The verification endpoint returns `Cache-Control: public, max-age=3600`; configure the CDN origins/distribution to preserve the one-hour TTL. A scheduled publisher should publish to at least two CDN origins; clients try primary CDN, secondary CDN, then the verification endpoint, and accept only a fresh, correctly signed active status.

Before discovery the CLI requests a run token bound to the license ID and a fresh random run nonce. Tokens expire within five minutes. After scanning, the CLI hashes the inventory JSON and requests a detached server signature. The server returns a signed receipt binding the customer, license, nonce, and SHA-256 hash. The Redis-backed verification service atomically reserves each nonce and consumes its token on the first output-signature request, preventing token reuse across instances. Revocation is checked again before signing output. No repository contents or repository data are sent to the licensing service. The service receives signed license claims (including customer and contractor IDs) to validate eligibility and includes the customer ID in the signed output receipt; it logs only the license ID and outcome, and keeps the customer ID in Redis only until the run token is consumed or expires.

The inventory file is now an object containing `CustomerId`, `LicenseId`, `RunNonce`, `Inventory` (the solution array), and `Receipt`. The receipt signature covers the serialized `Inventory` array. SharpShift analysis/report readers accept both this signed format and legacy inventory arrays.

### Runtime configuration

Set these environment variables for the CLI:

- `SHARPSHIFT_LICENSE_FILE`: path to the customer license JSON.
- `SHARPSHIFT_LICENSE_STATUS_PRIMARY_URL`: HTTPS base URL for the primary CDN status origin.
- `SHARPSHIFT_LICENSE_STATUS_SECONDARY_URL`: HTTPS base URL for the secondary CDN status origin.
- `SHARPSHIFT_LICENSE_VERIFICATION_URL`: HTTPS base URL for the verification service.

The client applies a three-second timeout to each request. URLs may use HTTP only for loopback development endpoints. The public verification key is not configurable at runtime; it is embedded in the binary.

Set these environment variables for each verification-service instance:

- `SHARPSHIFT_LICENSE_PUBLIC_KEY_PEM`: issuer public PEM.
- `SHARPSHIFT_LICENSE_SIGNING_KEY_PATH`: mounted path to the matching private PEM (use a managed secret mount in production).
- `SHARPSHIFT_LICENSE_STATUS_FILE`: path to the latest signed status document.
- `SHARPSHIFT_LICENSE_REDIS_CONNECTION`: shared Redis connection string used by all instances for atomic one-time nonce storage.
- `ASPNETCORE_URLS`: listener URL; terminate TLS at the service gateway/load balancer.

Run at least two service instances behind a redundant HTTPS gateway and use a highly available Redis deployment. Redis unavailability blocks token issuance/output signing rather than allowing offline use. The status-list publisher must refresh both CDN origins at least hourly. The self-hosted service, CDN, Redis, TLS gateway, and managed-key-vault deployment are operational resources and must be provisioned by the operator.

## Other notes
- MSBuild evaluation is best-effort; environments without MSBuild fall back to XML parsing.
- Dependency health checks are best-effort and non-fatal. Network/API failures are logged as warnings and do not stop analysis.
- Health checks currently target package dependencies with explicit versions. Dependencies without versions are not checked.
- External data sources include NuGet V3 and OSV; large scans may encounter external rate limits.
- Per-solution markdown renders `Risk Findings` directly under solution overview/summary metadata.
