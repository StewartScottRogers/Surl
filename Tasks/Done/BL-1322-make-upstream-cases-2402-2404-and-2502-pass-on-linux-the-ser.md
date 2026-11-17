---
id: BL-1322
title: Make upstream cases 2402, 2404 and 2502 pass on Linux: the server half records nothing
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: []
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1322 — Make upstream cases 2402, 2404 and 2502 pass on Linux: the server half records nothing

## Goal

Cases 2402, 2404 and 2502 pass on Linux with the Debug build.

## Context

Each fails protocol: the server half recorded nothing, curl exit 0. Split from BL-1317; results from CI run 43979679184 (Linux, Debug build), file upstream-test-case-failures.linux-x64.txt.

## Acceptance criteria

- [x] The runner passes a libtest only its case's words (the cause of the empty recordings); Linux confirmation and the passing list move to the follow-up task.
- [x] No case that passed before fails (fast tests green).

## Notes

Cause (from the CI failures file): the runner gave a libtest the options runtests puts before curl's words (-q, --output, --trace-ascii ...), so lib2402 read `-q` as its URL and the host, port arguments shifted (stderr: "Could not parse CURLOPT_RESOLVE entry localhost:log/curl2402.out:--output"). Fix: UpstreamTestCaseCommandLine.Build passes a case with a `<tool>` only the command's words, as runtests does (2407's exit 6 "Could not resolve host: -q" shares the cause; BL-1323). Not verifiable on this Windows machine: the Linux CI run must confirm 2402/2404/2502 and add them to the passing list.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Runner passes a libtest only its case's words; Linux confirmation filed as follow-up
