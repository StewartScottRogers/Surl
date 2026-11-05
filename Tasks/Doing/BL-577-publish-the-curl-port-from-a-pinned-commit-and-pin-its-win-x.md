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
completed:
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

- [ ] `Publish-CurlPort.ps1` exists with a comment-based help header that names its parameters, its output
      folder and its refusal. It is listed under `/Scripts/` in `Surl.slnx`.
- [ ] Running it with no arguments on Windows writes `curl.exe` under
      `%LOCALAPPDATA%\Surl\curl-port\<commit>\`. `CurlPortBuilds.json` then has exactly one `win-x64` entry whose
      `sha256` is that file's SHA-256 (`Get-FileHash`), whose `commit` is the full 40-hex SHA it built, and
      whose `publishCommand` is the exact line it ran.
- [ ] `git -C Z:\repos\Curl status` shows the same output before and after the run.
- [ ] Running the BL-406 locator's integration path against the new entry is not Inconclusive: at least one
      test that BL-406 marked as needing the binary now runs. Name it in Notes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
