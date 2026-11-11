---
id: BL-752
title: Trace BL-472's rows of the CoAP requirement matrix to their named tests
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-472]
touches: [Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-752 — Trace BL-472's rows of the CoAP requirement matrix to their named tests

## Goal

Every row of `Documentation/Conformance/Extended/Coap.md` that names BL-472 names, in its place, the
tests in `Surl.Extended.Protocol.Coap.UnitTests` that prove it, or the task that still owes it.

## Context

- ADR-0097, "Consequences": each task replaces its ID in the matrix with the named test.
- BL-472's tests: `CoapProtocolServerTests` (bytes written by hand from RFC 7252), `CoapFlowSessionTests`
  (separate responses, retransmission, ACK and RST matching, duplicates), `CoapResourceAnswererTests`
  (methods, conditions, response codes, access rules), `CoapRequestOptionsTests`,
  `CoapResourceDirectoryTests`, `CoapDuplicateCacheTests`.
- Rows BL-472 did not prove belong to BL-750 (a proxy request naming the listener, RFC 7252 section
  5.7.2) and BL-751 (decision 7's limits and `5.03`): name those tasks instead.

## Acceptance criteria

- [x] `Select-String -Path Documentation/Conformance/Extended/Coap.md -Pattern 'BL-472'` finds nothing.
- [x] Every test name the matrix gives exists in `Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests`.

## Notes

Filed by BL-472: the matrix file was outside its `touches`.

- 2026-11-11: Traced all 66 BL-472 rows to tests in `Surl.Extended.Protocol.Coap.UnitTests`, read test by test rather than by class name. Rows owing more than BL-472 proved now name BL-750 (proxy request naming the listener: 5.7.2-1, 5.10.2-2), BL-751 (5.03 with Max-Age: 4.7-1, 5.9.3.4-1) or BL-1037, filed here for the row parts no test proves (4.2-7 after the ACK, 5.1-1, 5.1-2 for GET, 5.4.4-1 absent defaults, 5.5-1 GET/DELETE payload, 5.10.8-2, 6.5-1 upper-case hex, RFC6690-4.1-1 percent-decoded filter).
- Row 5.4.4-1 says Max-Age 60 is sent despite being the default, because ADR-0097 decision 1 sends it on purpose for RFC 7641.
- Fixed a stale name the matrix already gave: `ServeAsync_ConRequest_IsAnsweredWithAPiggybacked404CarryingItsMessageIdAndToken` is now `ServeAsync_MissingResource_Gets404WithItsToken`, so criterion 2 holds for every name in the file (checked by a PowerShell scan of each backticked test name against the test project's methods).
- `dotnet build` could not run in this lane: `global.json` pins SDK 10.0.401, which is not installed (BL-1035). The task changes only Markdown (the matrix and two task files), which no project compiles, so it cannot change the build or test result.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Backlog. Released unstarted by the interactive session: 66 rows to trace one by one; better as a lane's full run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Every CoAP matrix row that named BL-472 now names the Coap.UnitTests tests that prove it, or BL-750, BL-751 or BL-1037 for what is still owed
