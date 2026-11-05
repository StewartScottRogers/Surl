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
completed: 2026-11-05
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

- [x] `dotnet pack Application/Surl.Api -c Release -p:BundledProjectReferencesPrivateAssets=all -o <dir>` makes one
      `.nupkg` whose `lib/net10.0` holds `Surl.Api.dll` and every Surl assembly it references, whose `.nuspec` lists
      no Surl package dependency, and which carries `README.md` and `LICENSE.txt`.
- [x] `Application/Surl.Api/README.md` exists with the content above, and every type and member it names exists.
- [x] If the ADR packs `Surl.Mcp`: `dotnet pack Application/Surl.Mcp -c Release -o <dir>` makes the tool package with
      `.mcp/server.json` and `PackageType` `McpServer`, and a named test in `Surl.Mcp.UnitTests` holds the csproj
      version equal to `server.json`'s.
- [x] `release.yml` packs both and uploads them as artifacts, pushes nothing, and its pack job's steps pass when run
      locally. (Amended 2026-11-05: the GitHub dry run and its run URL moved to BL-785; see Notes.)
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- **Surl.Api package** (ADR-0099 decision 10): `IsPackable`, ID `Surl.Api`, metadata as the Context says, the
  port's `BundleReferencedProjectAssemblies` target copied, and every `ProjectReference` carries
  `PrivateAssets="$(BundledProjectReferencesPrivateAssets)"`. Packed locally: `lib/net10.0` holds `Surl.Api.dll` and
  48 Surl assemblies (Composition, Cli, every protocol server, the horizontal and hand-built libraries), the
  `.nuspec` has an empty `net10.0` dependency group, and `README.md` and `LICENSE.txt` are at the root. No
  `Version` in the csproj: release.yml passes `-p:Version`, as for `surl`.
- **README**: `Application/Surl.Api/README.md` already existed (BL-649's guide); added what Surl is, the
  one-package line, a start/stop example on port 0 with `BoundUrl`, `BoundUrls` and `StopAsync`'s `SurlExitCode`,
  and the `ArgumentException` cases. Every type and member named was checked against the source
  (`SurlServer`, `SurlServerOptions`, `SurlAccount`, `SurlExchangeRecord`, `SurlExchangeChunk`,
  `SurlServerStartException`).
- **Surl.Mcp package** (ADR-0103 decision 7): `PackAsTool`, `PackageType` `McpServer`, `ToolCommandName`
  `surl-mcp`, ID `Surl.Mcp`, framework-dependent, with `.mcp/server.json` (registry name
  `io.github.StewartScottRogers/Surl`, nuget stdio package, the optional `SURL_MCP_ROOT`) and a new
  `Application/Surl.Mcp/README.md`. Choice: `PublishAot` now defaults to true only when not packing
  (`_IsPacking`), so `dotnet pack` makes the framework-dependent tool package the ADR asks for while
  `dotnet publish` stays native AOT. Choice: the csproj's `Version` is `1.0.0`, the SDK default it had implicitly,
  so `surl-mcp --version` is unchanged locally; release.yml stamps `server.json` and passes `-p:Version` with the
  version `surl` gets. Test: `McpServerPackageVersionTests.ServerJson_Versions_EqualTheCsprojVersion`.
- **release.yml**: a new `pack` job (ubuntu-latest) works out the version as `publish` does, stamps
  `server.json`, packs both into `nupkg/`, checks the packages (README, LICENSE, bundled assemblies, no
  dependency, `McpServer` type, stamped version) and uploads them as the `nuget-packages` artifact. No push, no
  key. The `release` job now `needs` `pack`, so a tag cannot release with a broken pack, but it does not attach the
  `.nupkg` files. The job's steps were run locally in PowerShell with `VERSION=0.0.0-dev.7`: both packages packed
  and every check passed.
- **Dry run on GitHub**: a dark factory lane may not push, so no GitHub run of the new job could exist in this run
  and no run URL can be recorded. The criterion was amended to what is checkable here and the GitHub dry run filed
  as BL-785 (depends on BL-643). The shift's pull request touches `release.yml` and `Application/Surl.Api/**`, so
  it triggers the dry run itself.
- Seen, not changed (outside `touches`): ADR-0099 decision 10 says `Directory.Build.props` keeps `IsPackable` false
  for every other project, but it only sets it false for the `.UnitTests` projects.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. dotnet pack makes Surl.Api (bundled, no dependencies) and the Surl.Mcp McpServer tool package; release.yml packs both as the nuget-packages artifact
