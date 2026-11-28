---
id: BL-1983
title: Decide how the surl command line is published to nuget.org as a .NET tool
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-27
completed:
---
# BL-1983 — Decide how the surl command line is published to nuget.org as a .NET tool

## Goal

An ADR, "Decided by Claude under Stewart's delegation", says how `surl` reaches a reader who
has only .NET and nuget.org: the package ID, the command name, the package shape and how the
release workflow pushes it.

## Context

Stewart decided on 2026-11-27 (relayed by the Surly book session) that the Surly book follows
the nuget.org packages, not the repositories: every recipe must run for a reader with only
.NET and nuget.org. Stewart asked on 2026-11-27 for `surl` to be published as a .NET tool.
From `v0.1.12`, `Surly.Surl.Api` (library) and `Surly.Surl.Mcp` (tool, command `surl-mcp`,
`PackAsTool` in `Application/Surl.Mcp/Surl.Mcp.csproj`) are on nuget.org through trusted
publishing in `.github/workflows/release.yml`. `Surl.Console` (assembly `surl`) is published
only as native AOT binaries on each GitHub release and is not packable
(`Directory.Build.props` sets `IsPackable` false).

Questions the ADR answers:
- The package ID (for example `Surly.Surl`, beside `Surly.Surl.Api` and `Surly.Surl.Mcp`) and
  the command name (`surl`).
- The shape: one framework-dependent tool package, or the .NET 10 SDK's runtime-specific tool
  packages (`ToolPackageRuntimeIdentifiers`) carrying the native AOT binaries per platform,
  with a framework-dependent fallback. Weigh start-up time, package size, platforms the
  release already builds, and that `dnx` and `dotnet tool install` must both work.
- How `release.yml` packs and pushes it under the existing trusted publishing policy, and that
  its version is the tag without its `v`, as for the other two packages.
- What `surl --aihelp`, the README and `DOWNLOAD.md` say about installing it (changed by
  BL-1984, not here).

Base class library only; no new package reference.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions` names the package ID, the command name,
      the package shape and the release workflow steps, with the reasons and the options
      weighed, and is marked "Decided by Claude under Stewart's delegation".
- [ ] The ADR cites ADR-0098 and the trusted publishing setup BL-1795 and BL-1797 left in
      `release.yml` (nuget.org user `StewartRogers86`).
- [ ] BL-1984's Context names the ADR and its decisions, so a lane can implement it without
      a question.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
