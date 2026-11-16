---
id: BL-1289
title: Record each Linux and macOS conformance test's outcome from a dispatched CI run
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Conformance/Conformance-Off-Windows.md]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1289 — Record each Linux and macOS conformance test's outcome from a dispatched CI run

## Goal

`Documentation/Conformance/Conformance-Off-Windows.md` records, for one named CI run, which `Surl.Conformance.UnitTests` tests passed, were Inconclusive or failed on the `linux-x64` and `osx-arm64` reference builds, so the ADRs that say "not yet measured on Linux or macOS" can cite a measurement.

## Context

- Milestones 2 to 5 of `Documentation/Planning/Roadmap.md` are "not closed" because what the Linux and macOS reference builds do is pinned as predicted but not recorded: SSH (ADR-0051 decision 2, ADR-0054 decision 16 item 4), mail (ADR-0053, ADR-0055, ADR-0056), WebSocket (ADR-0071), SMB (ADR-0073 decision 11) and RTSP on Linux (ADR-0074 decision 11). BL-1290 to BL-1294 record them in the ADRs from this file.
- CI's `Build and test` legs on `ubuntu-latest` and `macos-latest` already run every `Surl.Conformance` test but the upstream test cases (`.github/workflows/ci.yml`, step "Conformance tests") against the pinned reference builds, but a green run uploads no `.trx`: the step "Publish this attempt's test results" runs only when the run is dispatched with `attempts` above 1 (BL-1104).
- So: dispatch CI on the shift branch the lane pushes to, as BL-1142 did, never on `master` (the workflow's `concurrency` group would cancel master's own run): `gh workflow run CI --ref <shift branch> -f attempts=2`. Wait for it with `gh run watch <id>` (no `sleep`), then `gh run download <id> -n test-results-ubuntu-latest-attempt-1 -n test-results-macos-latest-attempt-1` into the session's scratch folder, never into the repository. Read outcomes from the `.trx` XML with PowerShell (`[xml]`); no Python.
- A passing test's `TestContext` lines are in the `.trx` (`Output/StdOut`); `UpstreamCurlLogsInToSurlOverSshTests` writes surl's `SSH negotiated` note there (`WriteNegotiatedAlgorithms`). Copy those lines for both platforms.
- If a test fails on either leg in attempt 1, look at attempt 2 too; record both and name the failure, but do not fix it here. File a task for any failure (lanes may file tasks).
- Stand-in: not applicable; this records upstream curl's results against `surl`, with no store, event or fault involved.

## Acceptance criteria

- [x] `Documentation/Conformance/Conformance-Off-Windows.md` exists and names the CI run (its URL), the commit it ran, and the two reference pins' SHA-256 from `UpstreamCurlBuilds.json`.
- [x] It has one table row per `Upstream*Tests` test class with the counts Passed, Inconclusive and Failed for `linux-x64` and for `osx-arm64`, at least for the classes behind SSH, SMTP, IMAP, POP3, WebSocket, SMB and RTSP.
- [x] Every Inconclusive and every failed test is listed by full name with the message the `.trx` gives for it.
- [x] The `SSH negotiated` lines `UpstreamCurlLogsInToSurlOverSshTests` wrote on each platform are quoted, with the test that wrote each.
- [x] No `.trx` or downloaded artifact is committed (`git status` shows only the new Markdown file under `Documentation/`).

## Notes

- Run https://github.com/StewartScottRogers/Surl/actions/runs/43906686682 (dispatched on `factory/phase-1`, `attempts=2`). Attempt 1 passed on both legs with no failure; attempt 2 on macOS was cancelled after 30 min, so it is noted in the document, not used.
- Choice: the per-class table covers every `Upstream*` conformance class (not the `CurlPort*`/`Pinned*` classes of the extended servers, which ADR-0003 keeps out of Surl's verdicts); Inconclusive and skipped tests are grouped by message with each full name listed once under it.
- Findings for BL-1290 to BL-1294: macOS RTSP (16) and OpenLDAP (47) are Inconclusive for want of a reference build; Linux passes both.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Recorded in Documentation/Conformance/Conformance-Off-Windows.md from CI run 43906686682
