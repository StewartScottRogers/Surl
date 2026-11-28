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
completed: 2026-11-27
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

- [x] `dotnet pack Application/Surl.Console -c Release -p:Version=0.0.1 -o nupkg` produces the
      package the ADR names, and installing it with `dotnet tool install --tool-path <dir>
      --add-source nupkg <package> --version 0.0.1` gives a `surl` that answers `surl --version`.
- [x] `release.yml` packs that package on a release tag, attaches it to the GitHub release
      beside the other two `.nupkg` files, and pushes it to nuget.org with the same login.
- [x] `dotnet publish Application/Surl.Console` still produces the native AOT binary.
- [x] `surl --aihelp`, `README.md` and `DOWNLOAD.md` give the nuget.org install command for
      `surl` (the `--aihelp` completeness tests pass).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: ADR-0191 is the plan, so no separate `protocol-architect` pass. No option, exit code or
  wire byte changes, so no `conformance-auditor` pass.
- Measured on Windows x64, 2026-11-27, with `NUGET_PACKAGES` in a scratch directory (since
  deleted, with the `nupkg` folder): `Surly.Surl.Console.0.0.1.nupkg` is 6,516,953 bytes as the
  first criterion's command packs it and 3,694,196 with the workflow's three switches. Both
  installed, and each installed `surl --version` began `surl 0.0.1 (win-x64)` and exited 0; the
  second also exited 2 for an unknown option. The workflow's new check of the package passed
  against it in PowerShell. `dotnet publish Application/Surl.Console` produced a native
  `surl.exe` (31,770,624 bytes), and `Test-PublishedSurl.ps1` passed all four checks on a
  `win-x64` publish.
- The workflow's pack and install steps have not run on Linux: the pull request dry run
  (`release.yml` triggers on `Application/Surl.Console/**`) is their first run, and the first
  tag after `v0.1.12` the first push. The documents therefore state publication as intent.
- Wording choice: `--aihelp` says "Each surl release after 0.1.12 is published to nuget.org",
  true before that release exists and in its own binary.
- Not measured: whether `dotnet dnx` asks for confirmation before it first downloads a package
  (a `code-reviewer` remark from memory). ADR-0191 already tells scripts to install the tool.
- Review: `code-reviewer` found nothing that had to be fixed; its tense fixes are applied.
- Fast tests: 30,380 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl packs as the .NET tool Surly.Surl.Console; release.yml packs, installs, attaches and pushes it from the next tag, and --aihelp and the documents give the install commands
