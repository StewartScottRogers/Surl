---
id: BL-1141
title: Find why upstream tests 1072, 1073 and 4001 fail errorcode on CI and file a task per cause
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1126]
touches: [Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1141 — Find why upstream tests 1072, 1073 and 4001 fail errorcode on CI and file a task per cause

## Goal

The cause of each of three CI-only failures is known from a recorded run and has a filed task:
upstream tests 1072 and 1073 (`errorcode` on Linux and macOS, pass on Windows) and 4001
(`errorcode` on macOS only, passes on Linux).

## Context

- Seen in CI run 43629075978 (head `356da7cd`, curl 8.21.0 reference pins). That run holds only the
  failing section: the CI log has no verdicts, and the results file says `test<N> fail errorcode`.
  BL-1126 (which this depends on) adds `upstream-test-case-failures.<platform>.txt`, with curl's
  actual exit code and stderr, to the `upstream-test-case-results-<platform>` artifact.
- What the cases are:
  - 1072 and 1073: `-T -` (chunked PUT from stdin) with `-u testuser:testpass --anyauth`; the reply
    is `HTTP/1.0` `401` or `301` with `swsclose`, and curl must exit 25 because a chunked body cannot be
    resent over HTTP/1.0. The `<protocol>` section already matches on Linux and macOS; only the
    exit code differs. The difference between platforms suggests when the HTTP half closes relative to
    the body it has read (for example a reset from closing with unread bytes), but that is a
    hypothesis to test, not a finding.
  - 4001: `--ech ecl:... -k https://...`; curl must exit 101 (`CURLE_ECH_REQUIRED`, "ECH was attempted
    but failed", https://curl.se/libcurl/c/libcurl-errors.html read 2026-11-14) when the server
    ignores ECH. Exit 25 in 1072 and 1073 is `CURLE_UPLOAD_FAILED`. The macOS and Linux reference pins are both
    OpenSSL 4.0.1 with ECH; only macOS fails.
- Steps:
  1. CI ignores pushes to `factory/**`; dispatch it: `gh workflow run CI --ref factory/phase-1`, find
     the run with `gh run list --workflow CI`, and download both artifacts with
     `gh run download <id> -n upstream-test-case-results-linux-x64` (and `-osx-arm64`).
  2. Read the three cases' blocks. If curl's exit or stderr does not settle the cause, measure the
     exchange with `Record-CurlExchange.ps1` and a pinned build, never the Curl port (ADR-0003).
  3. File one task per cause with `task-board.ps1 new` (lanes may file extra tasks), naming the cases,
     the platform, the expected and actual exit codes, and the fix's project; add each new ID to
     BL-1142's `depends-on`. A design question becomes a Claude `docs` ADR task first.
- This is a measurement and triage task. It changes no code.

## Acceptance criteria

- [ ] Notes hold the CI run URL and its head commit, which contains BL-1126.
- [ ] Notes give, for each of 1072, 1073 and 4001 on each failing platform, curl's actual exit code,
      its stderr's first line and the cause.
- [ ] Each cause has a filed task naming its cases, and BL-1142's `depends-on` lists each one.
- [ ] `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` has a dated paragraph naming the run
      and each cause with its task ID.

## Notes

## Log

- 2026-11-14: Created.
