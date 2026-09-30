---
id: BL-288
title: Read and write WebSocket frames and the handshake accept key in Surl.Protocol.Ws
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Ws.UnitLibrary, Surl.Protocol.Ws.UnitTests]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
---
# BL-288 — Read and write WebSocket frames and the handshake accept key in Surl.Protocol.Ws

## Goal

`Surl.Protocol.Ws.UnitLibrary` reads and writes RFC 6455 frames and computes the
`Sec-WebSocket-Accept` value, as internal types with no transport, so the handshake and message
tasks (BL-301, BL-302) build on a codec already held to the quality gates.

## Context

- This is groundwork decided by RFC 6455 alone; it needs no ADR and pins no byte upstream curl
  sends, so it runs beside the decision tasks. Where behaviour is a server choice (which close code
  a limit sends, what to do after a protocol error) it belongs to BL-285's ADR and BL-302, not here:
  the codec reports what it read and lets the caller decide.
- What to build (RFC 6455 section 5):
  - a frame reader over an `IConnection` (`Surl.Protocol.Abstractions`) or a byte buffer: `FIN`,
    `RSV1`-`RSV3`, opcode, `MASK`, the 7-bit, 16-bit and 64-bit payload lengths (the most
    significant bit of the 64-bit length must be 0, and the minimal encoding rule of section
    5.2), the masking key and unmasking (section 5.3); a frame whose length exceeds a maximum the
    caller gives is reported before any payload byte is read (ADR-0006: `--max-message` bounds a
    WebSocket frame); reads end cleanly at end of stream, and a truncated frame is reported as
    such;
  - the checks that make a frame invalid whatever the server wants: a reserved opcode (3-7,
    B-F), a control frame (close, ping, pong) with `FIN` clear or a payload over 125 bytes
    (section 5.5), a close payload of 1 byte, a close code outside section 7.4's allowed ranges,
    reported as a typed outcome, not an exception the caller must catch;
  - a message reassembler: continuation frames joined in order, control frames allowed between
    fragments (section 5.4), a continuation with no message started or a new data frame inside a
    message reported, the joined size bounded by the caller's maximum, text validated as UTF-8
    (section 8.1, including across fragment boundaries);
  - a frame writer for the server side (never masked, section 5.1), minimal length encoding,
    close frames with a code and a UTF-8 reason no longer than 123 bytes;
  - the accept key (section 4.2.2): base64 of SHA-1 over the key and
    `258EAFA5-E914-47DA-95CA-C5AB0DC85B11`, with the BCL's `SHA1`.
- Test vectors from the RFC: section 1.3's key `dGhlIHNhbXBsZSBub25jZQ==` gives
  `s3pPLMBiTxaQ9kYGzzdZDXLEp9s=`; section 5.7's examples (a single-frame unmasked text "Hello",
  a masked "Hello", a fragmented unmasked text, an unmasked and a masked ping, a 256-byte and a
  64 KiB binary frame header).
- Constraints: internal types with `InternalsVisibleTo` the tests (already in the csproj); async
  reads without `.Result`; complexity at most 10 per method (`CodeMetricsConfig.txt`); code may be
  copied from the Curl port's WebSocket code where it saves work, but every expected byte comes from
  the RFC (ADR-0003).

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Ws.UnitTests` read and write every RFC 6455 section 5.7 example and
      the section 1.3 accept-key example byte for byte, and pass.
- [x] Tests show each invalid-frame case in Context reported as its own outcome, a frame over the
      given maximum refused before its payload is read (the fake connection has delivered no payload
      byte when the outcome returns), and fragmented UTF-8 split inside a code point accepted.
- [x] `dotnet build Surl.Protocol.Ws.UnitLibrary -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ws.UnitLibrary`.

## Notes

- Built (all `internal`, namespace `Surl.Protocol.Ws`): `WebSocketFrameReader` over an
  `IConnection` returning `WebSocketFrameReadResult` with a `WebSocketFrameReadOutcome`;
  `WebSocketMessageReassembler` returning `WebSocketReassemblyStep` with a
  `WebSocketReassemblyOutcome`; `WebSocketFrameEncoder` (`EncodeFrame`, `EncodeClose`);
  `WebSocketAcceptKey.Compute`; `WebSocketCloseCodes.IsAllowedOnTheWire`; `WebSocketOpcode(s)`,
  `WebSocketFrame`, `WebSocketMessage`. Written fresh from RFC 6455, nothing copied from the Curl port.
- **The accept value in this task's Context was a typo.** RFC 6455 section 1.3 gives
  `s3pPLMBiTxaQ9kYGzzhZRbK+xOo=` for `dGhlIHNhbXBsZSBub25jZQ==`, not `...zzdZDXLEp9s=`. SHA-1
  agrees (checked by hand in PowerShell), and the test pins the RFC's value.
- Default taken: the frame limit counts the **whole frame, header included**, the way
  `MqttPacketReader` counts a whole MQTT packet under the same ADR-0006 row. 0 means no limit, and
  a length no array can hold (over `Array.MaxLength`) is `FrameTooLarge` either way. The limit is
  checked after the length bytes and before the masking key, so a refused frame's key and payload
  are never read.
- Default taken: close codes allowed on the wire are 1000-1003, 1007-1011 (section 7.4.1),
  1012-1014 (IANA registry, from the range section 7.4.2 reserves for it) and 3000-4999. 1004-1006,
  1015 and the rest of 0-2999 are `CloseCodeNotAllowed`. A close reason that is not UTF-8 is its own
  outcome, `CloseReasonNotUtf8` (sections 5.5.1 and 8.1).
- Left to the caller, as Context says: the RSV bits and the MASK bit are reported on the frame,
  not judged. Section 5.1 says an unmasked client frame fails the connection; that judgement
  belongs to BL-302.
- Default taken: text is validated as UTF-8 once the message is whole (`Utf8.IsValid`), so a
  code point split across fragments is accepted without an incremental decoder. The cost is that
  bad UTF-8 in an early fragment is not caught until the last one; the message size limit still
  bounds what is held.
- The encoder throws `ArgumentException` for a caller error (a control frame with FIN clear or
  over 125 bytes, a close reason over 123 UTF-8 bytes) and `ArgumentOutOfRangeException` for a
  close code not allowed on the wire. These are programming errors, not peer input.
- Pipeline: planned, tested and built in this session with no separate agent stages. The codec
  pins no byte upstream curl sends, so there is no conformance stage (Context).
- Verified: 84 tests in `Surl.Protocol.Ws.UnitTests` pass; `Measure-CodeQuality.ps1 -Library
  Surl.Protocol.Ws.UnitLibrary` reports 100% line, 100% branch, 34 members, 0 failing, worst CRAP 8;
  `dotnet build` is clean; all fast tests are green.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Ws reads, reassembles and writes RFC 6455 frames and computes Sec-WebSocket-Accept, at 100% coverage
