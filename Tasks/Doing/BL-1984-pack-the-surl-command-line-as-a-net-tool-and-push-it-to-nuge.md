---
id: BL-1984
title: Pack the surl command line as a .NET tool and push it to nuget.org on each release tag
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1983]
touches: [Application/Surl.Console, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, .github/workflows/release.yml, README.md, DOWNLOAD.md, Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1984 — Pack the surl command line as a .NET tool and push it to nuget.org on each release tag

## Goal

From the next release tag, a reader with only .NET installs and runs `surl` from nuget.org,
with `dotnet tool install -g <package>` or `dnx <package>`, as the ADR from BL-1983 decides.

## Context

Read
[ADR-0191](../../Documentation/Planning/Decisions/ADR-0191-the-surl-command-line-is-published-to-nuget-org-as-the-net-tool-surly-surl-console.md)
first (BL-1983); it is the specification. Its decisions:

- Package ID `Surly.Surl.Console`, command `surl`, assembly still `surl`.
- One framework-dependent tool package, no `ToolPackageRuntimeIdentifiers`. `Surl.Console.csproj`
  gets `IsPackable`, `PackAsTool`, `ToolCommandName`, `PackageId`, the metadata `Surl.Mcp.csproj`
  has, a new package `README.md` and `LICENSE.txt`, and
  `PublishAot` set only when `'$(PublishAot)' == '' And '$(_IsPacking)' != 'true'`.
- Version is the tag without its `v` (`-p:Version`).
- `release.yml`: the `pack` job packs it with the three publish switches that drop the libraries'
  symbol and XML files, checks its contents, installs it from `nupkg` and runs `surl --version`;
  the `release` job adds it to the `sha256sum` line and the push loop; comments say three packages
  and fifteen files. Native publish legs, `Test-PublishedSurl.ps1`, archives and installers do not
  change.
- Documents give `dotnet tool install -g Surly.Surl.Console` and
  `dotnet dnx Surly.Surl.Console -- <options> <url>` (ADR-0191 decision 5 lists what each says,
  `AiHelpProse.OverviewAbout`, Product-Overview and Roadmap included).

The push logs in as nuget.org user `StewartRogers86`; keep that login and `--skip-duplicate`.
`Surl.Console` must stay native AOT for the GitHub release binaries, and the AOT check in
`Directory.Build.props` must still pass. For the local install check, set `NUGET_PACKAGES` to a
scratch directory, so a test version `0.0.1` never stays in the machine's package cache.

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
- 2026-11-27: Backlog -> Doing.
