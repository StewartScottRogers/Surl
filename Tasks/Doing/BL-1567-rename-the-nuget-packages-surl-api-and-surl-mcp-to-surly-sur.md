---
id: BL-1567
title: Rename the NuGet packages Surl.Api and Surl.Mcp to Surly.Surl.Api and Surly.Surl.Mcp
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Api/Surl.Api.csproj, Application/Surl.Mcp/Surl.Mcp.csproj, .github/workflows/release.yml, Application/Surl.Api/README.md, Application/Surl.Mcp/README.md, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Documentation, CLAUDE.md, README.md]
requirement: none
created: 2026-11-21
completed:
---
# BL-1567 — Rename the NuGet packages Surl.Api and Surl.Mcp to Surly.Surl.Api and Surly.Surl.Mcp

## Goal

Every NuGet package Surl publishes has the ID `Surly.Surl.<name>`: `Surl.Api` becomes `Surly.Surl.Api` and `Surl.Mcp`
becomes `Surly.Surl.Mcp`, from the next release (v0.1.3). Every document that names a package to install uses the new ID.

## Context

- Stewart's decision, approved 2026-11-21 in session: "Surly.Surl.* would be the prefix." The scope he approved:
  - **Changes:** package IDs only, the release workflow's package file names, and every install instruction and
    document naming a package.
  - **Stays:** C# namespaces and types (`Surl.Api`, `SurlServer`), assembly names, the executables `surl` and
    `surl-mcp`, project and folder names, and the solution.
  - **Scope:** Surl only, not the Curl port's packages. Already-published v0.1.0..v0.1.2 packages are left as they
    were.
- Today: `Application/Surl.Api/Surl.Api.csproj:19` `<PackageId>Surl.Api</PackageId>`;
  `Application/Surl.Mcp/Surl.Mcp.csproj:31` `<PackageId>Surl.Mcp</PackageId>`. `.github/workflows/release.yml:351-389`
  packs both (`dotnet pack ... -o nupkg`) and names the files `nupkg/$id.$VERSION.nupkg` (`:361`).
- Root `CLAUDE.md` calls them "NuGet package `Surl.Api`" and "NuGet package `Surl.Mcp`" in Overview and Project naming.
  Those lines change to the new IDs; the namespace and assembly names stay.
- Files that mention `Surl.Api`/`Surl.Mcp` and may name the *package* (most mean the project or namespace; change only
  package mentions): `Application/Surl.Api/README.md`, `Application/Surl.Mcp/README.md`, `Application/*/CLAUDE.md`,
  `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`, `ManualText.cs` (and their tests), `Application/Surl.Mcp.UnitLibrary/AiHelpResourceProvider.cs`
  (and its test), `Documentation/Wiki/Glossary.md`, `Home.md`, `Surl-As-A-Stand-In.md`, the root `README.md`.

## Acceptance criteria

- [ ] `<PackageId>` is `Surly.Surl.Api` and `Surly.Surl.Mcp`; `dotnet pack Application/Surl.Api -c Release -p:Version=0.0.0-test -p:BundledProjectReferencesPrivateAssets=all -o <tmp>`
      and `dotnet pack Application/Surl.Mcp -c Release -p:Version=0.0.0-test -o <tmp>` produce `Surly.Surl.Api.0.0.0-test.nupkg`
      and `Surly.Surl.Mcp.0.0.0-test.nupkg`, and the `.nuspec` inside each has the new `<id>`.
- [ ] `release.yml` packs, checks and attaches the files under the new names; a `workflow_dispatch` dry run of
      `release.yml` on the shift branch (`dispatch-workflow.ps1`; park as Blocked "Waiting for run:<id>") uploads
      `Surly.Surl.Api.*.nupkg` and `Surly.Surl.Mcp.*.nupkg` artifacts.
- [ ] Every install instruction (`dotnet add package`, `PackageReference Include=`, `dotnet tool install`) and every
      sentence naming the *package* uses the new ID; a search for `PackageReference Include="Surl.` and
      `add package Surl.` finds nothing outside `Tasks/Done` and ADRs.
- [ ] A new ADR in `Documentation/Planning/Decisions` records the rename: "Decided by Stewart, 2026-11-21", with the
      scope above and why namespaces and assemblies stay. Root `CLAUDE.md`'s package mentions are updated to match.
- [ ] `dotnet build` clean and fast tests green (the aihelp and manual tests included).

## Notes

- Don't rename namespaces, assemblies, projects or folders. Stewart approved the package ID only.
- If a `.nupkg` name check in `release.yml` hard-codes an ID, change it. Don't add a second, old-named package.

## Log

- 2026-11-21: Created. Stewart approved the rename in session.
- 2026-11-21: Backlog -> Doing.
