# ADR-0187 — Surl publishes its NuGet packages as Surly.Surl.Api and Surly.Surl.Mcp

- **Status:** Accepted
- **Date:** 2026-11-21
- **Decided by:** Stewart, 2026-11-21, in session ("Surly.Surl.* would be the prefix."), carried out in BL-1567.
- **Builds on:** [ADR-0099](ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md) decision 10,
  [ADR-0103](ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md).

## Decision

Every NuGet package Surl publishes has the ID `Surly.Surl.<name>`, from the next release (v0.1.3):
`Surl.Api` becomes `Surly.Surl.Api` and `Surl.Mcp` becomes `Surly.Surl.Mcp`.

- **Changes:** the `<PackageId>` of both packages, the package file names `release.yml` packs and checks, the
  `identifier` in `Application/Surl.Mcp/.mcp/server.json`, and every install instruction or sentence naming a package.
- **Stays:** C# namespaces and types (`Surl.Api`, `SurlServer`), assembly names, the executables `surl` and
  `surl-mcp`, project and folder names, and the solution. The package ID is how a feed names the package; the rest is
  how the code names itself, and renaming it would break every consumer's `using` for no gain.
- **Scope:** Surl only, not the Curl port's packages. Packages already published as v0.1.0 to v0.1.2 are left as they were.

## Consequences

- ADR-0099 and ADR-0103 still name the old IDs as history; this ADR supersedes only that detail.
- No second, old-named package is packed.
