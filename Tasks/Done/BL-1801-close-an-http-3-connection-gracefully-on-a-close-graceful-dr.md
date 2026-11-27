---
id: BL-1801
title: Close an HTTP/3 connection gracefully on a close=graceful drop fault, as ADR-0188 says
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1794]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Core.UnitLibrary, Surl.Core.UnitTests, Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Surl.Quic.UnitLibrary, Surl.Quic.UnitTests, Documentation/Planning/Decisions, Surl.Conformance.UnitTests, Surl.Cli.UnitLibrary, Surl.Api, Documentation/Wiki/Surl-As-A-Stand-In.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1801 — Close an HTTP/3 connection gracefully on a close=graceful drop fault, as ADR-0188 says

## Goal

An `Accept`-stage drop with `after-bytes` and `close=graceful` on an HTTP/3 listener closes the QUIC connection with
`CONNECTION_CLOSE` and no error after byte N, and its `FaultAppliedEvent` says `Graceful`.

## Context

Found while planning BL-1794. ADR-0188's HTTP/3 row promises the QUIC transport's graceful close, but
`Foundation/Surl.Core.UnitLibrary/ServingEngine.Datagrams.cs` wraps every datagram flow, QUIC included, in
`SilencedAfterBytesDatagramFlow` and ignores `FaultRule.Close`, so an HTTP/3 connection is only silenced and its event
says `Reset` (ADR-0188, amendment 4). Stand-in use: a test cuts an HTTP/3 download cleanly and asserts what the client
saw. If the work is too big for one run, split it.

## Acceptance criteria

- [x] A graceful `after-bytes` rule on HTTP/3 sends `CONNECTION_CLOSE` with no error after the N bytes, pinned by a unit
      test over an in-memory datagram flow.
- [x] That drop's `FaultAppliedEvent` has `Close = FaultClose.Graceful`.
- [x] TFTP and every other plain datagram flow still publish `FaultClose.Reset`, pinned by a test.
- [x] ADR-0188 has an amendment recording the change.
- [x] What pinned upstream curl does with the cut is measured and recorded there, or the amendment says no pinned build
      speaks HTTP/3.

## Notes

- Design (ADR-0188 amendment 5): `IGracefulCutDatagramProtocolServer` (marker, `Http3ProtocolServer` only) and
  `IGracefullyCutDatagramFlow`; the engine counts datagrams (`GracefullyCutAfterBytesDatagramFlow`) and
  `Surl.Quic`'s `QuicApplicationConnection` sends `CONNECTION_CLOSE` `NO_ERROR` (`CloseWithNoError`) at the cut.
  Datagrams are never split, so the client gets at most N bytes; close datagrams are never counted.
- Pins: `QuicDatagramFlowServerTests.GracefulCut`,
  `Http3ProtocolServerTests.ServeAsync_FlowCutByAGracefulDropFault_ClosesTheConnectionWithNoErrorInPlaceOfTheAnswer`,
  `ServingEngineTests.ServeAsync_GracefulDropRuleOnADatagramServerWithItsOwnClose_CutsTheFlowAndPublishesGraceful` and
  `..._WithoutItsOwnClose_BehavesAsAResetDrop` (TFTP, `Reset`).
- Measured (pinned static curl 8.21.0, Windows): graceful cut exit 56 at once (`ERR_DRAINING`), reset exit 28 at
  `--max-time`, cut before the handshake ends exit 7. Not 18. Linux and macOS not measured, so the three
  `Integration` tests are Windows only.
- Added to `touches` (no other task in Doing named them): `Surl.Conformance.UnitTests` (the measurement),
  `Surl.Cli.UnitLibrary` (the `--aihelp` fault paragraph must describe the new behaviour), `Surl.Api` (`SurlFault.Close`
  doc and README) and the stand-in wiki page.
- Pipeline: plan, tests and implementation were done in the session to save tokens; `code-reviewer` reviewed. Its
  findings (a racy test, the HTTP/3 test cutting before the request, a connection's own close counting towards N) are
  fixed.
- Follow-up filed: BL-1802 (DNS over QUIC and WebTransport).
- Gates: build clean, `dotnet format` clean, 30145 fast tests passed (3 skipped), HTTP/3 and cut-download integration
  tests 26 passed (4 skipped), `Measure-CodeQuality.ps1` 0 failing members for Core, Quic and Protocol.Http.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. A close=graceful after-bytes drop on HTTP/3 closes the QUIC connection with CONNECTION_CLOSE NO_ERROR and its event says Graceful; pinned curl 8.21.0 exits 56.
