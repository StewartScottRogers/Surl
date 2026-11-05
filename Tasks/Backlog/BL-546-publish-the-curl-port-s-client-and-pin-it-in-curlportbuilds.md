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
completed:
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

- [ ] `CurlPortBuilds.json` has a `win-x64` entry with all six fields, its `sha256` the SHA-256 of
      the published `curl.exe`, and its `commit` a commit on the port's `master`.
- [ ] `dotnet test --filter "FullyQualifiedName~UpstreamCurlBuildPinsTests|FullyQualifiedName~CurlPortClientPinsTests"`
      passes, including `Parse_RepositoryPinFiles_ShareNoSha256WithCurlPortBuilds`.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
