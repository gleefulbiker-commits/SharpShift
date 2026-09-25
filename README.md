# SharpShift Inventory

SharpShift Inventory — small CLI to discover solutions/projects, evaluate frameworks, and produce a JSON inventory.

Features
- Discover solutions and projects under a folder.
- Normalize target frameworks (SDK and legacy).
- Evaluate projects using MSBuild when available (reflection-based) with XML fallback.
- Extract dependencies (PackageReference and Reference) including version, hint path, public key token, and additional metadata.

Recent changes (this commit)
- Dependency metadata is now preserved as structured fields:
  - `HintPath` (string) — captured for assembly references.
  - `PublicKeyToken` (string) — parsed from reference include when present.
  - `OtherMetadata` (Dictionary<string,string>) — key/value pairs for remaining metadata (e.g., `PrivateAssets`).
  - `Metadata` (string) remains for backward compatibility (concatenated `key=value` pairs).
- `ProjectInventory` now includes a `Dependencies` list so dependency details are serialized into `inventory.json`.
- Added a best-effort unit test (`ProjectEvaluatorTests`) that asserts MSBuild-evaluated item metadata keys when MSBuild is available; the test skips in environments without item entries.

How to run
- `dotnet build`
- `dotnet test`
- Run the CLI runner to produce `inventory.json` in a folder (see `InventoryCliRunner`):
  - `dotnet run --project src/SharpShift.Inventory.Cli -- <rootPath> <outputFile>`

Notes
- MSBuild evaluation is best-effort; environments without MSBuild will fall back to XML parsing. The new test is skipped in those environments.
- The inventory JSON now includes dependency details (`HintPath`, `PublicKeyToken`, `OtherMetadata`) for richer reporting.
