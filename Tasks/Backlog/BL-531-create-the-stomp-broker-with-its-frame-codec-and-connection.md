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
completed:
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

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - the specification's example frames read and written byte for byte;
      - a header with each escape decoded, and an undefined escape refused;
      - a repeated header keeping its first value;
      - a body with a NUL read by `content-length`;
      - CONNECT `accept-version:1.0,1.1,1.2` answered with `version:1.2`;
      - no common version gets ERROR with a `version` header;
      - bad credentials get ERROR and close;
      - a frame over the size limit gets ERROR and close.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
