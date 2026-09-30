---
id: BL-302
title: Exchange WebSocket messages, pings and closes after the upgrade in Surl.Protocol.Ws
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-301]
touches: [Surl.Protocol.Ws.UnitLibrary, Surl.Protocol.Ws.UnitTests]
requirement: FR-048
created: 2026-09-30
completed:
---
# BL-302 — Exchange WebSocket messages, pings and closes after the upgrade in Surl.Protocol.Ws

## Goal

After the upgrade, `WsProtocolServer` sends what BL-285's ADR decides (for example the content-store
file at the request path) and answers every frame upstream curl can send - `PING` with `PONG`,
`CLOSE` with `CLOSE`, data messages whole or fragmented, and every protocol error with its RFC 6455
close code - within the ADR's limits.

## Context

- Decision: BL-285's ADR (what is sent after the upgrade and in which opcode, how a directory path is
  answered, the answer to each client frame, the close codes, `--max-message` for a frame and a
  reassembled message, the idle timeout and maximum duration, the verbose and trace notes).
- Code: BL-288's frame reader, reassembler and writer; BL-301's `WsProtocolServer`. Content through
  `Surl.Content.UnitLibrary/ContentStore.cs` if the ADR serves files - the csproj gains the
  `Surl.Content.UnitLibrary` reference, which ADR-0002's table allows - with the exposure defaults
  (ADR-0006 section 2, ADR-0015).
- Time: the injected `TimeProvider` for the idle timeout and maximum duration, and
  a hand-written test `TimeProvider` (the test projects each keep a `ManualTimeProvider`; no package);
  never `Thread.Sleep`. ADR-0059 tells a limit from shutdown.
- Fixtures: the client frames of BL-285's cases (the masked `PONG` and `CLOSE` curl sends, and the
  libcurl-sent frames if BL-285's ADR pinned `libcurl-4.dll` and they are recorded by then), plus
  RFC 6455 section 5.7's examples for frames upstream has not been recorded sending.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Ws.UnitTests` show, over `InMemoryConnection`: the ADR's messages sent
      after the `101`; a `PING` answered by a `PONG` with the same payload; a client `CLOSE` echoed
      and the connection closed; a fragmented message reassembled; an unmasked client frame, a
      reserved opcode, an oversized control frame, invalid UTF-8 text, a frame past `--max-message`,
      the idle timeout and the maximum duration each closed with the ADR's code; no test opens a
      socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ws.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
