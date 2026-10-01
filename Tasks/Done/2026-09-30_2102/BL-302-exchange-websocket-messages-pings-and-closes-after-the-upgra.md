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
completed: 2026-09-30
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

- [x] Tests in `Surl.Protocol.Ws.UnitTests` show, over `InMemoryConnection`: the ADR's messages sent
      after the `101`; a `PING` answered by a `PONG` with the same payload; a client `CLOSE` echoed
      and the connection closed; a fragmented message reassembled; an unmasked client frame, a
      reserved opcode, an oversized control frame, invalid UTF-8 text, a frame past `--max-message`,
      the idle timeout and the maximum duration each closed with the ADR's code; no test opens a
      socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ws.UnitLibrary`.

## Notes

- **Built** (ADR-0071 decisions 4 to 7): `WebSocketExchange` runs after the `101`. It keeps one
  client frame read pending, so client frames are answered between the frames surl sends. A file
  goes as one binary message in 65536-byte frames, each read from the content store as a byte
  range; a listing as one text message; then an empty `CLOSE`, a half-close and the one-second
  linger. `WsProtocolServer(contentStore, policy, echoesMessages: true)` is `--ws-echo`'s server;
  BL-303 registers the option.
- **Check 10 now uses `ContentStore.GetEntryStatus`** rather than the mapping's kind, so a file
  asked for with a trailing `/` (which `CopyFileBytesAsync` refuses) is a `404`, and the length
  the frames are planned from comes from the same lookup.
- **Choices with a sensible default** (not in ADR-0071, recorded here):
  - A file that cannot be read once upgraded is answered `CLOSE` `1011` ("internal error", RFC
    6455 section 7.4.1), noted `Closing with 1011: <path> could not be read (<why>)`.
  - `--max-message` counts the payload alone. The frame reader counts the header too, so it is
    given `--max-message` + 14 (the longest client header). A frame up to 13 bytes past the limit
    has its payload read, and is then refused `1009` by the reassembler. Either way the answer is
    `1009`.
  - The frame reader judges a frame's header before the server sees the mask and `RSV` bits. So
    an unmasked frame that is also past `--max-message` is answered `1009`, not `1002`.
  - While surl echoes a message, a client frame that arrives is answered once the echo's last
    frame is sent. While it sends a file, the frame is answered before surl's next frame.
  - A client that half-closes without a `CLOSE` is noted. Under `--ws-echo` surl then
    half-closes without a `CLOSE`, since there is nothing to answer. While a file is sent, the
    file and the empty `CLOSE` still go out.
  - Limits: the engine's cancellation for the idle timeout and for the maximum duration look the
    same to a server (ADR-0059), so both are answered `CLOSE` `1001`. It is written within its own
    one-second deadline that only shutdown cuts short, then lingered on `ShutdownToken`.
- **Fixtures** recorded 2026-09-30 from pinned curl 8.21.0 (`Fixtures/README.md`). `file-chat`:
  curl wrote `chat` with exit 0 for exactly what surl now sends for `/chat`. `ping-pong`: curl's
  masked `PONG`, replayed to show a client `PONG` is ignored.
- Not amended in ADR-0071: BL-284 (in Doing) touches `Documentation/Planning/Decisions`.
- Quality: `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ws.UnitLibrary` reports 100% line,
  100% branch, 99 members, 0 failing, worst CRAP 10. Ws tests: 184 passed.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. WsProtocolServer sends the path's file or listing (or echoes under --ws-echo), answers PING, CLOSE and every invalid frame with ADR-0071's codes, and closes 1001 at a limit
