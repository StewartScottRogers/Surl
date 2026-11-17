---
id: BL-1335
title: Read case 2407's Linux timeout output and record its cause
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1334]
touches: []
requirement: none
created: 2026-11-16
completed:
---
# BL-1335 — Read case 2407's Linux timeout output and record its cause

## Goal

The cause of case 2407's timeout on Linux is read from the CI artifact and recorded.

## Context

BL-1334 makes a timed-out client's stdout and stderr land in `upstream-test-case-timeout-output.<platform>.txt` inside the `upstream-test-case-results-<rid>` artifact. The first CI run on master after BL-1334 merges produces it for `linux-x64`: download it with `gh run download`, read the `test2407` block, and record the cause in `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md`. If the cause is a Surl defect, file a fix task. Case 2407 runs `lib2405` (curl_multi_waitfds over HTTP/2 through nghttpx to Surl).

## Acceptance criteria

- [ ] The `test2407` block of the Linux timeout output file is read and its cause recorded in `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md`.
- [ ] A fix task is filed when the cause is a Surl defect.

## Notes

Split from BL-1334, whose third criterion needs a CI run that exists only after BL-1334 merges.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. BL-1334 is not on master yet; the latest master CI run (44004609185) has no upstream-test-case-timeout-output file. Retry after the shift's merge PR carrying BL-1334 passes CI.
- 2026-11-16: Backlog -> Doing.
