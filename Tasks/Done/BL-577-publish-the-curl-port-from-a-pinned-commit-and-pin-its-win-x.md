---
id: BL-577
title: Publish the Curl port from a pinned commit and pin its win-x64 client in CurlPortBuilds.json
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-406]
touches: [Publish-CurlPort.ps1, CurlPortBuilds.json, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-577 — Publish the Curl port from a pinned commit and pin its win-x64 client in CurlPortBuilds.json

## Goal

A PowerShell script at the repository root, `Publish-CurlPort.ps1`, publishes the Curl port's `curl`
executable from a named commit into the folder ADR-0083 decision 2 names, and records the result in
`CurlPortBuilds.json`. It has been run once, so `CurlPortBuilds.json` holds a `win-x64` entry that BL-406's
`CurlPortClientLocator` accepts on this machine.

## Context

- ADR-0083 decision 2 sets the pin. The entry fields are `platform`, `path`, `sha256`, `commit`,
  `publishCommand` and `published`. The default folder is `%LOCALAPPDATA%\Surl\curl-port\<commit>\` on Windows
  and `~/.local/share/surl/curl-port/<commit>/` elsewhere. Building Stewart's own repository is not an upstream
  curl download and needs no approval. Re-pinning is an ordinary commit to `CurlPortBuilds.json`. BL-406
  creates `CurlPortBuilds.json` and its reader. Use the field names that BL-406 shipped.
- The port: https://github.com/StewartScottRogers/Curl, local clone `Z:\repos\Curl`. Its executable project
  is `Curl.Console` (assembly `curl`), and it publishes native AOT by default. Linking needs
  `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH for vswhere, which is the same need
  `Surl.Console` has. The port's `Curl.Console.csproj` comment documents the publish line.
- **Never build inside `Z:\repos\Curl`'s working tree.** The port may have its own dark factory running in it.
  The script makes its own clone (or a `git worktree` of a fetched clone) under the output folder's parent,
  checks out the commit with a detached HEAD, and publishes from there. It never commits, pushes or edits a
  file in the port's repository.
- The script takes `-Commit` (default: the current `origin/master` of `https://github.com/StewartScottRogers/Curl`,
  resolved to a full SHA) and `-Platform` (default `win-x64`). It refuses to write an entry whose SHA-256
  equals any `UpstreamCurlBuilds.json` entry. It replaces the existing entry for the same platform.
- Root scripts are listed in `Surl.slnx`'s `/Scripts/` folder, beside `Record-CurlExchange.ps1`. Add this one.
- PowerShell only, never Python (root `CLAUDE.md`).

## Acceptance criteria

- [x] `Publish-CurlPort.ps1` exists with a comment-based help header that names its parameters, its output
      folder and its refusal. It is listed under `/Scripts/` in `Surl.slnx`.
- [x] Running it with no arguments on Windows writes `curl.exe` under
      `%LOCALAPPDATA%\Surl\curl-port\<commit>\`. `CurlPortBuilds.json` then has exactly one `win-x64` entry whose
      `sha256` is that file's SHA-256 (`Get-FileHash`), whose `commit` is the full 40-hex SHA it built, and
      whose `publishCommand` is the exact line it ran.
- [x] `git -C Z:\repos\Curl status` shows the same output before and after the run.
- [x] Running the BL-406 locator's integration path against the new entry is not Inconclusive: at least one
      test that BL-406 marked as needing the binary now runs. Name it in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `Publish-CurlPort.ps1` fetches the one commit (`git init` + `git fetch --depth 1 <url> <sha>` + detached
  checkout) into `<OutputRoot>\.source\<commit>`, finds `Curl.Console.csproj`, publishes from the source root,
  hashes with `Get-FileHash`, refuses a SHA-256 any `UpstreamCurlBuilds.json` entry pins, replaces the
  platform's entry and deletes the source folder. Choices: a shallow per-commit fetch rather than a kept mirror
  (nothing to keep in sync, fast); the entry's `path` names `%LOCALAPPDATA%` (`%HOME%` elsewhere) when the default
  root is used, as BL-546's entry did, so the pin holds on any account; `publishCommand` is the literal line run,
  with the expanded output folder, as the acceptance criterion asks; the JSON is written by hand (keeps the
  file's layout and avoids Windows PowerShell 5.1's escaping of the comment's apostrophes as Unicode escapes).
- Ran it with no arguments: port `origin/master` = `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256
  `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`; it replaced BL-546's entry (commit 7051feb9).
  `git -C Z:\repos\Curl status` was identical before and after.
- BL-406 marked no test as needing the binary: nothing calls `PinnedCurlPortClientOnThisMachine.RunAsync` yet,
  and `Conformance/Surl.Conformance.UnitTests` is held by BL-389 (in Doing), so no test was added here. Instead
  the locator's integration path was run against the new entry with a throwaway `dotnet run locate.cs` that
  references `Surl.Conformance.UnitLibrary`: `CurlPortClientLocator.Locate` returned available ("...curl.exe is
  the pinned Curl port client for win-x64, published from commit b42c319b..."), and `CurlPortClientRunner` ran
  `--version`, exit 0. The pin-file Integration tests (`CurlPortClientPinsTests.Parse_RepositoryPinFile_ParsesWithoutFailure`,
  `UpstreamCurlBuildPinsTests.Parse_RepositoryPinFiles_ShareNoSha256WithCurlPortBuilds` and peers) pass, 4/4.
  Filed BL-594 to add the permanent Integration test once that project is free.
- `dotnet build -warnaserror` clean; fast tests 9571 passed, 0 failed across 42 test assemblies.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Publish-CurlPort.ps1 publishes the Curl port from a pinned commit; CurlPortBuilds.json pins win-x64 commit b42c319b, which the locator finds and runs
