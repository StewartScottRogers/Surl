---
id: BL-1078
title: Hold the HTTP test case connection open for upstream test 1548 (BL-683 closes it)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-12
completed:
---
# BL-1078 — Hold the HTTP test case connection open for upstream test 1548 (BL-683 closes it)

## Goal

Upstream test 1548 passes again on the Windows reference pin: its curl waits and exits 28, as the case expects.

## Context

BL-683 (8b24ef3d) closes the HTTP case server connection when its reply part is empty. The run of BL-562 (2026-11-12, win-x64) shows test 1548 failing errorcode: curl exited 0, expected 28; the log says the case has no data part or an empty one and the connection was closed with no bytes. 1548 is on the win-x64 passing list, so the integration suite reports it as a regression. Decide which empty-reply cases hold the connection open (measure upstream sws with Record-CurlExchange.ps1) and keep BL-683 cases passing.

## Acceptance criteria

- [ ] Test 1548 passes in UpstreamTestCasesAgainstSurlTests on win-x64, and the cases BL-683 fixed still pass.
- [ ] dotnet build -warnaserror is clean and the fast tests are green.

## Notes

- 2026-11-12 (Claude, at Stewart's request): the read block is lifted. `.claude/settings.json` now anchors the data deny to the repository root (`Read(/data/**)`, `Edit(/data/**)`) and allows `Read(./Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/**)`; CLAUDE.md says the same. A fresh session read `UpstreamTestCases/data/test1548`. A new lane run picks it up.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Blocked. Stewart: this session's permissions deny reading Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test*, so I cannot see which empty-reply cases (1548 vs 470/1244) need the connection held; allow reading that folder and move this back to Backlog.
- 2026-11-12: Blocked -> Backlog. Stewart said yes on 2026-11-12: lanes may read UpstreamTestCases/data now; see Notes.
- 2026-11-12: Backlog -> Doing.
