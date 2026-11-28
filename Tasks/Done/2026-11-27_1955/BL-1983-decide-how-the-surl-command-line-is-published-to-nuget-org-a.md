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
completed: 2026-11-27
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

- [x] A new ADR in `Documentation/Planning/Decisions` names the package ID, the command name,
      the package shape and the release workflow steps, with the reasons and the options
      weighed, and is marked "Decided by Claude under Stewart's delegation".
- [x] The ADR cites ADR-0098 and the trusted publishing setup BL-1795 and BL-1797 left in
      `release.yml` (nuget.org user `StewartRogers86`).
- [x] BL-1984's Context names the ADR and its decisions, so a lane can implement it without
      a question.

## Notes

- ADR-0191: ID `Surly.Surl.Console`, command `surl`, one framework-dependent tool package.
  `Surly.Surl.Console` rather than this task's example `Surly.Surl`, because ADR-0187, Stewart's
  own decision, gives every package the ID `Surly.Surl.<name>`.
- Measured with SDK 10.0.404 on Windows x64 (figures in the ADR) by editing
  `Surl.Console.csproj` temporarily; the file was restored and is unchanged.
- This task's Context says `Directory.Build.props` makes `Surl.Console` not packable. It does
  not: that `IsPackable` false is for test projects, and an unchanged pack makes a library
  package `surl`. The ADR records the measured behaviour.
- BL-1984 edited beyond its Context: `touches` gained `Surl.Cli.UnitLibrary` and its tests
  (`--aihelp` lives there), `Product-Overview.md` and `Roadmap.md` (both say `surl` is not a
  .NET tool).
- Docs pipeline: written in the session, not delegated to `align-and-document`, since the work
  is the decision itself. No `.cs` or project file changed, so no build or test run (`/verify`
  is never run for docs).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0191 decides surl ships to nuget.org as the framework-dependent .NET tool Surly.Surl.Console (command surl), with the release.yml steps; BL-1984 carries them.
