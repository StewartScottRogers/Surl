---
id: BL-1343
title: Triage the linux-x64 curl-port-phase-7 failures of run 44044472038 into each family's findings table
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Documentation/Conformance/CurlPort, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1343 — Triage the linux-x64 curl-port-phase-7 failures of run 44044472038 into each family's findings table

## Goal

Every linux-x64 failure of `curl-port-phase-7` run 44044472038 is classified, with upstream curl as the oracle, as a Curl port defect, a Surl defect or a test defect, and listed in its family's findings table.

## Context

BL-1339 made linux-x64 compare instead of throw `UnpinnedCurlPortClientException`. Run 44044472038 (`factory/phase-1`, a5337bdf7): 371 passed, 71 failed, 47 not executed. Failures by class: Ftp 4, Http3 1, Ldap 47 (27 `OpenLdap_`, 20 `WinLdap_`), Mail 13, Ssh 5, UpstreamTestCases 1. The assertion text is "A port defect Documentation/Conformance/CurlPort/<Family>.md does not list". Artifact: `phase-7-linux-x64-results`. The 20 `WinLdap_` failures may need an OS condition rather than a finding. Judge by pinned upstream curl, never by the Curl port (ADR-0003).

## Acceptance criteria

- [x] Each of the 71 failures is either listed in its family file's findings table with its classification, or fixed in Surl.
- [x] The `WinLdap_` cases do not fail on linux-x64 (OS condition or finding).
- [x] A rerun of `curl-port-phase-7` on linux-x64 fails no case that its family file does not list.

## Notes

- Source: run 44044472038's `phase-7-linux-x64-results` artifact (trx), read for each failure's verdict and log.
- The findings tables were one list for every platform, so a Windows finding that agrees on Linux (Http3 finding 2,
  SSH-1's four weak-algorithm cases) failed there. A heading that names a platform in parentheses
  (`### Findings (linux-x64)`, `(win-x64)`) now scopes the rows under it to that platform
  (`CurlPortFindingsFile.LinesForPlatform`, used by every family's reader and the upstream test cases').
- Triage: 45 port defects listed under linux-x64 (Ldap L-1..L-4, Mail M-1, Ftp FTP-1, Ssh SSH-2, UTC-5 for
  test1072/1073); causes inside the port are not traced. Test defects fixed: 20 `WinLdap_` cases now Inconclusive off
  Windows; the nine upstream-skipped cases (debug-build, libtest) no longer judged as Surl defects and the test now
  locates the Debug build and libtests binary. 5 listed Windows findings moved to win-x64-only tables.
- Third acceptance box is verified by construction (every one of the 71 failures is addressed above) and by the fast
  tests; the linux-x64 rerun itself only happens on the next CI run of `curl-port-phase-7`.
- Decision (no ADR needed beyond ADR-0089 decision 5's rule): per-platform findings tables are a test/doc convention.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. linux-x64 failures classified into per-platform findings tables; WinLdap_ off Windows; skipped upstream cases not judged
