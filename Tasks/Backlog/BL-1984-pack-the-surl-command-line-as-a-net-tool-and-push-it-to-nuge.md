---
id: BL-1984
title: Pack the surl command line as a .NET tool and push it to nuget.org on each release tag
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1983]
touches: [Application/Surl.Console, .github/workflows/release.yml, README.md, DOWNLOAD.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1984 — Pack the surl command line as a .NET tool and push it to nuget.org on each release tag

## Goal

From the next release tag, a reader with only .NET installs and runs `surl` from nuget.org,
with `dotnet tool install -g <package>` or `dnx <package>`, as the ADR from BL-1983 decides.

## Context

BL-1983's ADR names the package ID, command name, package shape and release steps; read it
first. `Application/Surl.Mcp/Surl.Mcp.csproj` (`PackAsTool`, `ToolCommandName surl-mcp`,
`PackageId Surly.Surl.Mcp`) and the `dotnet pack` and nuget.org push steps in
`.github/workflows/release.yml` (BL-1789, BL-1795, BL-1797) are the pattern to follow. The
push logs in as nuget.org user `StewartRogers86`; keep that login and `--skip-duplicate`.
`Surl.Console` must stay native AOT for the GitHub release binaries, and the AOT check in
`Directory.Build.props` must still pass.

## Acceptance criteria

- [ ] `dotnet pack Application/Surl.Console -c Release -p:Version=0.0.1 -o nupkg` produces the
      package the ADR names, and installing it with `dotnet tool install --tool-path <dir>
      --add-source nupkg <package> --version 0.0.1` gives a `surl` that answers `surl --version`.
- [ ] `release.yml` packs that package on a release tag, attaches it to the GitHub release
      beside the other two `.nupkg` files, and pushes it to nuget.org with the same login.
- [ ] `dotnet publish Application/Surl.Console` still produces the native AOT binary.
- [ ] `surl --aihelp`, `README.md` and `DOWNLOAD.md` give the nuget.org install command for
      `surl` (the `--aihelp` completeness tests pass).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
