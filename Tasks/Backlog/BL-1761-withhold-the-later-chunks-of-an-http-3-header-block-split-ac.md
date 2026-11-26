---
id: BL-1761
title: Withhold the later chunks of an HTTP/3 header block split across QUIC STREAM frames from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1718]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1761 — Withhold the later chunks of an HTTP/3 header block split across QUIC STREAM frames from the exchange log

## Goal

No byte of an HTTP/3 request `HEADERS` payload carrying a secret field is logged in clear, however QUIC splits the frame.

## Context

- Found by reading the code in BL-1718, not recorded on the wire.
- `QuicStream.ReceiveData` reports each in-order piece of a stream as one chunk, and `Surl.Core`'s
  `EventPublishingDecryptedBytesSink` redacts every chunk with a fresh redactor (ADR-0182 amendment 1).
- `Http3RequestBytesRedactor` withholds the part of a `HEADERS` payload that is in the first chunk, then reads the next
  chunk as frames from its first byte, so the rest of the QPACK section is logged as received, a secret included. This
  affects the http and https servers over HTTP/3 and WebTransport.
- A redactor kept per stream, or told the stream id, is one way; ADR-0182 amendments 4 and 14 describe the current form.

## Acceptance criteria

- [ ] A fast test splits a `HEADERS` frame carrying `authorization` into two received chunks of one stream and shows no byte of the field value in the notes.
- [ ] A fast test shows chunks of two streams interleaved do not disturb each other's redaction.
- [ ] An ADR-0182 amendment states the rule, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
