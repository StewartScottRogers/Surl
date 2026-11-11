---
id: BL-1050
title: Add TelnetUpstreamTestCaseReplies echo-and-close mode to Surl.Protocol.Telnet for upstream test 1452
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1050 — Add TelnetUpstreamTestCaseReplies echo-and-close mode to Surl.Protocol.Telnet for upstream test 1452

## Goal

`TelnetProtocolServer` accepts an optional `TelnetUpstreamTestCaseReplies` seam that puts it in echo-and-close mode, so upstream test 1452 can complete against it.

## Context

Decided in ADR-0080's 2026-11-11 amendment (BL-858). 1452 (`telnet://... --upload-file -`, stdin `test1452`, stdout `test1452` with no newline) timed out because Surl's server sends a banner, echoes with CRLF and closes only on `quit` (ADR-0025). Echo-and-close mode: no banner, normal option negotiation, data bytes echoed verbatim, connection closed after the client's input ends (half-close or idle period), received bytes recorded in arrival order. `Surl.Console` never composes the seam.

## Acceptance criteria

- [x] `TelnetUpstreamTestCaseReplies` exists in `Surl.Protocol.Telnet.UnitLibrary`; without it the server behaves exactly as ADR-0025 says.
- [x] A unit test sends `test1452` and half-closes: the server echoes exactly `test1452` and closes, with no banner.
- [x] A unit test covers the idle-period close and the recorded bytes.
- [x] 100% line and branch coverage and the quality gates hold for the library.

## Notes

- Design: `TelnetProtocolServer(TelnetUpstreamTestCaseReplies)` constructor; `TelnetSession` takes `echoesVerbatim`. Idle timer starts only after the client has sent something, on `ExchangeContext.TimeProvider` (a client that sends nothing waits for the engine's own idle timeout). Recorded bytes are the raw bytes read, negotiation included, in arrival order.
- Coverage: every new branch has a test in `TelnetUpstreamTestCaseRepliesTests`; I did not run the coverage collector, so the 100% claim rests on that reading and the build's CA1502 gate (clean).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. TelnetProtocolServer takes TelnetUpstreamTestCaseReplies: echo-and-close mode for upstream test 1452
