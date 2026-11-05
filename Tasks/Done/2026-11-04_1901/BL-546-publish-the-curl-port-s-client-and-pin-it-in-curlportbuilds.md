---
id: BL-546
title: Publish the Curl port's client and pin it in CurlPortBuilds.json
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-406]
touches: [CurlPortBuilds.json]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-546 — Publish the Curl port's client and pin it in CurlPortBuilds.json

## Goal

`CurlPortBuilds.json` pins at least one published Curl port client (win-x64 first), so
`PinnedCurlPortClientOnThisMachine` finds it instead of marking the extended servers' integration
tests inconclusive.

## Context

- ADR-0083 decision 2: clone or fetch https://github.com/StewartScottRogers/Curl, check out a
  commit of its `master`, publish its `curl` executable (`Curl.Console`, assembly `curl`; native AOT
  by default, needs `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH for vswhere)
  into `%LOCALAPPDATA%\Surl\curl-port\<commit>\` (Windows) or
  `%HOME%/.local/share/surl/curl-port/<commit>/` elsewhere - outside both repositories - and record
  `platform`, `path`, `sha256`, `commit`, `publishCommand` and `published`. Building Stewart's own
  repository is not an upstream curl download and needs no approval; re-pinning is an ordinary
  commit, not an ADR.
- BL-406 tried this in a dark factory lane on 2026-11-04 and the session's permissions refused
  the clone-and-publish command, so it shipped the file with an empty `builds` list. Run this in a
  session whose permissions let it clone into a temporary folder and run `dotnet publish` there;
  do not check out a commit in Stewart's own `Z:\repos\Curl` working tree.
- A path may name environment variables as `%NAME%`; `CurlPortClientLocator` expands them.

## Acceptance criteria

- [x] `CurlPortBuilds.json` has a `win-x64` entry with all six fields, its `sha256` the SHA-256 of
      the published `curl.exe`, and its `commit` a commit on the port's `master`.
- [x] `dotnet test --filter "FullyQualifiedName~UpstreamCurlBuildPinsTests|FullyQualifiedName~CurlPortClientPinsTests"`
      passes, including `Parse_RepositoryPinFiles_ShareNoSha256WithCurlPortBuilds`.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- Cloned the port's `master` into a temporary folder (not `Z:\repos\Curl`); HEAD was
  `7051feb900be7ecb5b810eab587879abc5a5cffd`. Published with
  `dotnet publish Curl.Console -c Release -r win-x64 -o %LOCALAPPDATA%\Surl\curl-port\<commit>`
  (native AOT, vswhere's folder on PATH). `curl.exe` SHA-256
  `3644DC0C0C23986C4CE65D4DF4D49AF0427B7D81C4AA2A1E277C7712E84399C5`; it is in no
  `UpstreamCurlBuilds.json` entry.
- The pinned `path` uses `%LOCALAPPDATA%` rather than Stewart's literal profile path, so the
  pin stays true on any Windows account that publishes the same commit (choice: the default
  ADR-0083 names). Dropped "No client is pinned yet." from the file's comment, since one now is.
- Only win-x64 is pinned; Linux and macOS pins need a publish on those machines and are left to
  whoever runs there (their integration tests stay inconclusive until then).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. CurlPortBuilds.json pins the Curl port's win-x64 client (commit 7051feb9), so the extended servers' integration tests find it
