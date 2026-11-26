---
id: BL-1762
title: Withhold the later chunks of an HTTP/3 header block split across QUIC STREAM frames from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1718]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1762 — Withhold the later chunks of an HTTP/3 header block split across QUIC STREAM frames from the exchange log

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

- [x] A fast test splits a `HEADERS` frame carrying `authorization` into two received chunks of one stream and shows no byte of the field value in the notes.
- [x] A fast test shows chunks of two streams interleaved do not disturb each other's redaction.
- [x] An ADR-0182 amendment states the rule, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

- Design (ADR-0182 amendment 21): `IStreamReceivedBytesRedactor` lets the engine keep one redactor that follows each
  stream; `Http3RequestBytesRedactor` implements it. `IDecryptedBytesSink.ReceivedFramePayload` tells an HTTP/3 `DATA`
  payload apart from the stream's next bytes, which share a layer and stream ID.
- `touches` widened with `Foundation/Surl.Protocol.Abstractions.UnitLibrary` and `.UnitTests`: without that contract
  change the engine cannot tell the two reports apart. No other task in `Doing` names them.
- A split `HEADERS` payload is withheld, not held back until whole: no buffer, and nothing reports a stream's end.
- `Surl.Core.UnitTests` now references `Surl.Http3.UnitLibrary`, so the first criterion is proved on the engine's notes
  with the real redactor. The review suggested a fake instead; kept the real one for that reason.
- Pipeline: plan, tests and implementation were done in the lane session, not delegated (a small change; saves tokens).
  The tests were written first but not run before the implementation; they name members that did not exist yet.
  `code-reviewer` reviewed it. No conformance stage: no byte a client receives changes.
- Review findings left open, filed: BL-1773 (High; pre-existing leak in the `Decoded` note for a malformed request) and
  BL-1774 (Low; WebTransport stream data read as frames, and abandoned streams stay remembered). Both lose or leak
  nothing new on the split-`HEADERS` path this task covers; BL-1774's log loss can now span chunks.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. No byte of a secret-carrying HTTP/3 HEADERS payload is noted in clear however QUIC splits it: the engine keeps one redactor that follows each stream (ADR-0182 amendment 21).
