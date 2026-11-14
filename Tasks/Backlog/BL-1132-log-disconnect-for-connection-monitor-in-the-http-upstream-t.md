---
id: BL-1132
title: Log [DISCONNECT] for connection-monitor in the HTTP upstream test-case server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Conformance/Surl.Conformance.UnitLibrary]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1132 — Log [DISCONNECT] for connection-monitor in the HTTP upstream test-case server half

## Goal

The HTTP upstream test-case server half implements the `connection-monitor` server command: when
a case's `<servercmd>` has it, the half appends `[DISCONNECT]` and a line ending to the bytes it
records once a client connection ends. Upstream tests 48, 206, 338, 1008, 1134, 1418, 1419 and 1421
then run instead of skipping `servercmd-unsupported:connection-monitor`, and pass.

## Context

- Skipped on every platform today: all 8 on Linux and macOS (CI run 43629075978), and 48, 338, 1134,
  1419 and 1421 on Windows (local reference run, 2026-11-14, `5a45202a`); 206, 1008 and 1418 skip a
  missing feature on Windows first.
- Upstream's definition, `docs/tests/FILEFORMAT.md` at tag `curl-8_21_0`, HTTP `<servercmd>`:
  "`connection-monitor` - When used, this logs `[DISCONNECT]` to the `server.input` log when the
  connection is disconnected." Test 48's `<protocol>` shows the result: two `HEAD` requests, then a
  line `[DISCONNECT]` (its `crlf="headers"` leaves that line ending as the case writes it; check the
  checker's handling against 48 and 1421).
- Where: `Protocols/Http/Surl.Protocol.Http.UnitLibrary` - `HttpUpstreamTestCaseReplies` (parses the
  commands, holds `ReceivedBytes`), `HttpUpstreamTestCaseServer`, and
  `HttpUpstreamTestCaseServerCommands.Supported`. Then add `connection-monitor` to the `Http` set in
  `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs` (one row, and its
  test's expected set).
- One marker per ended connection, whichever side closed it. The http/2 and http/3 gateways forward to
  this half, so the marker follows the half's connection, not curl's.
- Stand-in: not applicable. The seam is composed only by `Surl.Conformance.UnitTests`;
  `Surl.Console` never composes it (ADR-0080 decision 2).

## Acceptance criteria

- [ ] A fast test in `Surl.Protocol.Http.UnitTests` replays test 48's two requests over a fake
      `IConnection` with `connection-monitor` set, ends the connection, and finds `[DISCONNECT]` after
      the second request in `ReceivedBytes`; without the command it is absent.
- [ ] `UpstreamTestCaseServerCommands` lists `connection-monitor` for the `http` family, and its test
      pins it.
- [ ] A local Windows run of `UpstreamTestCasesAgainstSurlTests` passes 48, 338, 1134, 1419 and 1421
      and every case already on `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [ ] `dotnet build -warnaserror` is clean for the three projects, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
