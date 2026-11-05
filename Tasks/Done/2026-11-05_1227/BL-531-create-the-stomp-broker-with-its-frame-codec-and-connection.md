---
id: BL-531
title: Create the STOMP broker with its frame codec and connection negotiation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-530, BL-404]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-531 — Create the STOMP broker with its frame codec and connection negotiation

## Goal

`Surl.Extended.Protocol.Stomp.UnitLibrary` exists. It reads and writes STOMP frames, with the 1.2 header escapes,
repeated headers (the first wins), `content-length` bodies that contain NULs, CRLF or LF line ends, and heart-beat
EOLs between frames. It answers CONNECT or STOMP with CONNECTED, or with ERROR and close.

## Context

- Decision: BL-530's ADR, decisions 1, 2, 3 and 8. Rules: BL-543's ADR.
- New projects in `Protocols/Messaging/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.Stomp`.
  References: `Surl.Protocol.Abstractions` only.
- STOMP 1.2: "STOMP Frames" (the frame layout and the NUL terminator), "Value Encoding" (`\r`, `\n`, `\c`, `\\`,
  undefined escapes are fatal; CONNECT and CONNECTED headers are not escaped), "Repeated Header Entries", "Size
  Limits", "Connecting" (`accept-version`, `host`, `login`, `passcode`, `heart-beat`; CONNECTED's `version`,
  `heart-beat`, `session` and `server`), and "Protocol Negotiation".
- Logins through the `Surl.Protocol.Abstractions` contracts.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - the specification's example frames read and written byte for byte;
      - a header with each escape decoded, and an undefined escape refused;
      - a repeated header keeping its first value;
      - a body with a NUL read by `content-length`;
      - CONNECT `accept-version:1.0,1.1,1.2` answered with `version:1.2`;
      - no common version gets ERROR with a `version` header;
      - bad credentials get ERROR and close;
      - a frame over the size limit gets ERROR and close.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Plan: ADR-0118 decisions 0 to 3, 6 and 8, built after `Surl.Extended.Protocol.Nats` (BL-525) as the pattern:
  `StompFrameReader` (frames, escapes, repeats, `content-length`, limits), `StompFrameWriter`, `StompHeaderEscaping`,
  `StompVersionNegotiation`, `StompHeartBeat`, `StompErrors`, `StompServer` (with the refusal writer) and
  `StompServerConnection`. `ProtocolIsolationTests` finds the new library by its name, so it needed no edit.
- Tests for each criterion:
  - spec examples: `StompFrameReaderTests.ReadFrameAsync_SpecificationConnectExample_IsReadWhole`,
    `ReadFrameAsync_SpecificationSendExample_ReadsAndWritesByteForByte`, and `StompFrameWriterTests.Encode_Specification*`;
  - escapes: `StompFrameReaderTests.ReadFrameAsync_HeaderWithEachEscape_IsDecoded`, `ReadFrameAsync_UndefinedEscape_IsMalformedFrame`;
  - repeated header: `StompFrameReaderTests.ReadFrameAsync_RepeatedHeader_KeepsTheFirstValue`;
  - NUL body: `StompFrameReaderTests.ReadFrameAsync_BodyWithNulAndContentLength_IsReadByContentLength`;
  - version 1.2: `StompServerConnectionTests.ServeAsync_ConnectAcceptingEveryVersion_IsAnswered12`;
  - no common version: `StompServerConnectionTests.ServeAsync_NoCommonVersion_IsVersionErrorAndClosed`;
  - bad credentials: `StompServerConnectionTests.ServeAsync_BadCredentials_IsAuthenticationFailedAndClosed`;
  - size limit: `StompServerConnectionTests.ServeAsync_FrameOverTheSizeLimit_IsFrameTooLargeAndClosed`.
- Choices taken (defaults, inside ADR-0118):
  - CONNECT is judged in the order version, `heart-beat`, login, so no login is judged for a frame that would be refused
    anyway; 1.0 ignores `heart-beat`, which it does not have.
  - Until BL-532, SEND and the other routing frames are answered ERROR `Unknown command` and the connection, after
    CONNECTED, reads one frame (DISCONNECT or an ERROR); BL-532 turns that into a loop.
  - The body limit is `--max-message` alone; the bus's `--max-filesize` joins it in BL-532, which has the bus.
  - A part frame left at the client's close is dropped with no answer, as the NATS reader does.
  - Linger after an ERROR: writes are completed, then reads are discarded until end of stream or 1 s on the injected
    `TimeProvider`.
- `Documentation/Conformance/Extended/Stomp.md` still names BL-531 on its rows: `Documentation/Conformance` was in another
  Doing task's `touches`, so BL-828 traces those rows to the tests above.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Extended.Protocol.Stomp reads and writes STOMP 1.0-1.2 frames and answers CONNECT/STOMP with CONNECTED or ERROR and close
