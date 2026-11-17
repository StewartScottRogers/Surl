---
id: BL-1337
title: Add curl's HTTP/2 trace and the gateway's exchange log to case 2407's Linux timeout output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1337 — Add curl's HTTP/2 trace and the gateway's exchange log to case 2407's Linux timeout output

## Goal

When a libtest case times out on Linux, `upstream-test-case-timeout-output.<platform>.txt` also holds curl's HTTP/2 trace and the `http/2` gateway's exchange log lines, so the cause of 2407's missing stream 1 answer can be read.

## Context

BL-1336 could not find the cause by reading code: two concurrent `Upgrade: h2c` connections served from in-memory connections both answer stream 1 (test `Test2407_TwoUpgradedConnectionsServedAtOnce_EachGetsHeadersAndDataOnStream1`, passes). The Linux timeout output from CI run 44009482486 stops at curl's `Copied HTTP/2 data in stream buffer ... len=21`, with no trace of whether curl sent its preface or what the gateway did next. Run the debug libtest with `CURL_DEBUG=all` (or `--trace-config`-equivalent for libtest) and append the gateway's `ExchangeContext` log notes for the case to the timeout output file. Then read the next Linux run's `test2407` block and file the fix.

## Acceptance criteria

- [x] A timed-out libtest case's timeout output file holds curl's HTTP/2 trace lines.
- [x] It also holds the exchange log notes the `http/2` gateway wrote for that case.
- [x] A unit test in `Surl.Conformance.UnitTests` pins both.

## Notes

- `CURL_DEBUG=all` is set for the admitted libtest cases only (`UpstreamTestCaseLibtests.AdmittedNumbers`), so the other cases' stderr checks are unchanged. The exchange log notes are `serverHalves.Notes`. The unit test pins the file format; the live trace appears on the next Linux CI run (follow-up: read test2407 block).


## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Timeout output holds curl trace and exchange log notes
