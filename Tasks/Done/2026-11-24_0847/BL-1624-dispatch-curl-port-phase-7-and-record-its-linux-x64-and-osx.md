---
id: BL-1624
title: Dispatch curl-port-phase-7 and record its linux-x64 and osx-arm64 outcome in each Phase 7 family file
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-1622, BL-1623]
touches: [Documentation/Conformance/CurlPort]
requirement: FR-057
created: 2026-11-24
completed: 2026-11-24
---
# BL-1624 — Dispatch curl-port-phase-7 and record its linux-x64 and osx-arm64 outcome in each Phase 7 family file

## Goal

A `curl-port-phase-7` run made after BL-1622 and BL-1623 is recorded, with per-family counts, in the `linux-x64` and `osx-arm64` sections of every family file in `Documentation/Conformance/CurlPort/`, and the false osx-arm64 statements there are gone.

## Context

Run it with `.claude/skills/task-board/dispatch-workflow.ps1 -Id BL-1624 -Workflow curl-port-phase-7 -Ref <shift branch>` and park this task with `move -To Blocked -Reason "Waiting for run:<id> (curl-port-phase-7)"` (task-board skill, "Waiting on a workflow run"); the coordinator returns it to Backlog when the run completes. Read the `phase-7-linux-x64-results` and `phase-7-osx-arm64-results` artifacts (`gh run download <id>`), or the job logs' `Total tests` / `Passed` / `Failed` / `Skipped` lines and each `Failed <test>` entry.

What the family files say now, and why it must change:
- Every family file's `## osx-arm64` section (from BL-1324, run 44000208980) says "`UpstreamCurlBuilds.json` pins no upstream curl build for `osx-arm64`". That is false: `UpstreamCurlBuilds.json` pins an `osx-arm64` reference build (SHA-256 `04E0E69B…EEBF`, curl 8.21.0, pinned 2026-09-28). The real cause was the port hash: in run 44044472038 the osx-arm64 job logged 357 `UnpinnedCurlPortClientException` failures, which BL-1622 removes.
- Line 3 of each family file names run 44044472038 (2026-11-17) as the current linux-x64 outcome. The later scheduled run 45198867449 (2026-11-23, `master` at `986449355167a9c352b9d4a0fdb6bda17e9d0239`) had 400 passed, 21 failed, 68 skipped on linux-x64; this task's run supersedes both.
- The osx-arm64 reference build lists no `ldap` and no `rtsp` protocol, so the LDAP and RTSP families are expected to be Inconclusive on osx-arm64; say so with the locator's message rather than as a finding.

Record only; classifying failures is BL-1625 (osx-arm64) and BL-1626 (linux-x64). The Curl port is never an oracle (ADR-0003); the workflow gates nothing (ADR-0089 Amendment 8).

## Acceptance criteria

- [x] A `curl-port-phase-7` run dispatched by this task, on a commit that contains BL-1622 and BL-1623, completed with both test jobs run (not skipped or cancelled); its run ID and Surl commit are in this task's Notes.
- [x] Each family file's `## osx-arm64` section names that run, the Surl commit, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, and the family's passed / failed / not-executed counts, and no longer says that no upstream curl build is pinned for osx-arm64.
- [x] Each family file's `## linux-x64` section, and its line-3 summary, name that run and the family's counts in the same form.
- [x] `CurlPortBesideUpstreamCurlOverHttp2Tests` shows executed (not "not executed") cases on both platforms, as recorded in `Http2.md`; if it does not, the reason is written in `Http2.md` and a follow-up task is filed.
- [x] The LDAP and RTSP families' osx-arm64 sections state the Inconclusive reason the run logged.

## Notes

- 2026-11-24: Dispatched curl-port-phase-7 on factory/phase-1 (head c8eb2e1e9, contains BL-1622 a808996a4 and BL-1623 e326c72ce): run 45412760425. Record its results when it completes.

- Read-back routes (ADR-0164 decisions 1 to 4 and 7, BL-1203), all `GET`: `/v1/mail/{owner}`, `/v1/mail/{owner}/{mailbox}`
  (`after_uid`, `limit`), `/v1/mail/{owner}/{mailbox}/{uid}` (`message/rfc822` bytes with `X-Surl-Uid` and `X-Surl-Flags`, or JSON
  for `Accept: application/json`), `/v1/files/{path}` (bytes, or a folder listing when the path ends in `/`; `after_name`,
  `limit`), `/v1/queues/{name}/messages` (`after_position`, `limit`), `/v1/topics/{name}/retained` and `/v1/topics/{name}/log`
  (`from_offset`, `limit`). `IControlTarget` gains the matching read members; an absent item is `ControlRefusalKind.NotFound`
  (404), one past 16 MiB is the new `ControlRefusalKind.TooLarge` (413 `too_large`). Reads publish nothing.
- 2026-11-17 (fourth attempt): BL-1340 was pushed; run 44037418915 (factory/phase-1, cdf417e9f) set SURL_CURL_PORT_CI_SHA256=048AAEE0... yet linux-x64 still threw UnpinnedCurlPortClientException: the env var was read only by `PinnedCurlPortClientOnThisMachine.LocateAsync`, while `PinnedCurlPortBesideUpstreamCurl`, the Mail and Ssh port-beside-upstream tests and `PinnedCurlPortLibcurlBesideUpstreamLibcurl` called `Locate`/`LocateLibrary` without it. Fixed: all four now pass `PinnedCurlPortClientOnThisMachine.CiAcceptedSha256` (touches gained Conformance/Surl.Conformance.UnitTests). Conformance fast tests 1035/1035. Next: the shift pushes this commit, then `gh workflow run curl-port-phase-7.yml --ref factory/phase-1`, read the linux-x64 test job, record the outcome in each family file's linux-x64 section.
- 2026-11-24: Recorded run 45412760425 (success, both test jobs ran; Surl commit 70828fe7e0becfaef1aeec787cdb7c42023732f7, which contains BL-1622 and BL-1623; the dispatch note's c8eb2e1e9 was the branch head then). Counts per family come from the two `.trx` artifacts (see each family file). `CurlPortBesideUpstreamCurlOverHttp2Tests` executed 15 cases on both platforms (5 passed, 10 failed), so no follow-up task. Extended and Libcurl have no result in the artifact (their classes are not in it). Old osx-arm64 sections were replaced; the old linux-x64 sections are kept under "Earlier run". Failures are recorded only; BL-1625 and BL-1626 classify them.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Blocked. Waiting for run:45412760425 (curl-port-phase-7)
- 2026-11-24: Blocked -> Backlog. Run 45412760425 completed: success; read it with gh run view 45412760425
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Run 45412760425 recorded with per-family counts in the linux-x64 and osx-arm64 sections of every Phase 7 family file; false osx-arm64 pin claim removed
