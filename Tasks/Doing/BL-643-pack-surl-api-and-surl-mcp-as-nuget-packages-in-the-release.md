---
id: BL-643
title: Pack Surl.Api and Surl.Mcp as NuGet packages in the release workflow's dry run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-617, BL-637]
touches: [Application/Surl.Api, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests, .github/workflows/release.yml]
requirement: none
created: 2026-11-04
completed:
---
# BL-643 — Pack Surl.Api and Surl.Mcp as NuGet packages in the release workflow's dry run

## Goal

`dotnet pack` turns `Surl.Api` into one NuGet package a .NET test project can reference with nothing else to
restore, and `Surl.Mcp` into the MCP server package BL-612's ADR decides, and `release.yml` packs both and keeps them
as workflow artifacts - so that publishing them later is one approved push, which this task does not make.

## Context

- Decisions: BL-611's ADR decision 10 (`Surl.Api`'s package ID, version, and bundling its project references into
  the package's `lib` folder) and BL-612's ADR decision 7 (whether `Surl.Mcp` is also a NuGet MCP server package:
  `PackAsTool`, `PackageType` `McpServer`, `.mcp/server.json`, per-runtime native packages). If BL-612's ADR decided
  `Surl.Mcp` is not packed, pack only `Surl.Api`, say so in Notes, and drop `Application/Surl.Mcp` from the change.
- Port models, copyable but never evidence (`git -C Z:\repos\Curl show origin/master:<path>`):
  `Curl.Api/Curl.Api.csproj` (the `BundleReferencedProjectAssemblies` target and
  `-p:BundledProjectReferencesPrivateAssets=all`), `Curl.Mcp/Curl.Mcp.csproj` and `Curl.Mcp/.mcp/server.json`, and
  the port's ADR-0596.
- Package metadata: author Stewart Scott Rogers, licence file `LICENSE.txt` (MIT), project URL
  https://github.com/StewartScottRogers/Surl, a package `README.md` in each project (for `Surl.Api` it is the
  agent- and human-facing guide BL-611's ADR decision 9 names: what it is, a start/stop example on port 0, the
  options, the exceptions and exit codes).
- No `dotnet nuget push`, no API key, no tag: publishing a package is a release action that needs Stewart (root
  `CLAUDE.md`, "Git and GitHub"). The workflow uploads the `.nupkg` files as artifacts only.

## Acceptance criteria

- [ ] `dotnet pack Application/Surl.Api -c Release -p:BundledProjectReferencesPrivateAssets=all -o <dir>` makes one
      `.nupkg` whose `lib/net10.0` holds `Surl.Api.dll` and every Surl assembly it references, whose `.nuspec` lists
      no Surl package dependency, and which carries `README.md` and `LICENSE.txt`.
- [ ] `Application/Surl.Api/README.md` exists with the content above, and every type and member it names exists.
- [ ] If the ADR packs `Surl.Mcp`: `dotnet pack Application/Surl.Mcp -c Release -o <dir>` makes the tool package with
      `.mcp/server.json` and `PackageType` `McpServer`, and a named test in `Surl.Mcp.UnitTests` holds the csproj
      version equal to `server.json`'s.
- [ ] `release.yml` packs both and uploads them as artifacts, pushes nothing, and a dry run (run URL in Notes) passes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
