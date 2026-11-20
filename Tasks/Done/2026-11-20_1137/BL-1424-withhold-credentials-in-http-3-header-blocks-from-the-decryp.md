---
id: BL-1424
title: Withhold credentials in HTTP/3 header blocks from the decrypted-bytes notes
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1422]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1424 — Withhold credentials in HTTP/3 header blocks from the decrypted-bytes notes

## Goal

With `--http3` and `--trace`, the `authorization` and `proxy-authorization` values of an HTTP/3
request never appear in the hex decrypted-bytes notes in a recoverable form; they are written as
BL-1417's ADR says.

## Context

- HTTP/3 plaintext reaches the log through `IDecryptedBytesSink`
  (`Protocols/Http/Surl.Http3.UnitLibrary/Http3RequestStreamServer.cs` and
  `Protocols/Http/Surl.Quic.UnitLibrary/QuicStream.cs`), written by
  `Foundation/Surl.Core.UnitLibrary/EventPublishingDecryptedBytesSink.cs` as
  `<layer> bytes received, stream <n>, <len> bytes: <hex>` (ADR-0169). QPACK field lines are
  recoverable from that hex.
- BL-1419 routes the decrypted-bytes note through the redactor as the ADR says; this task supplies the
  QPACK-aware redactor (`Protocols/Http/Surl.Qpack.UnitLibrary` decodes field sections) and has the
  HTTP/3 server hand it out. `Surl.Protocol.Http.UnitLibrary` is touched only for the one-line wiring of
  `Http3ProtocolServer.cs`.

## Acceptance criteria

- [x] A redactor for HTTP/3 request streams in `Protocols/Http/Surl.Http3.UnitLibrary`. Fast tests in
      `Protocols/Http/Surl.Http3.UnitTests` show a HEADERS frame whose field section carries
      `authorization` (literal with name reference, and Huffman-coded) noted in the ADR's form.
- [x] A request without such a field is noted byte for byte as before.
- [x] `Http3ProtocolServer` hands the redactor out (one named test in
      `Protocols/Http/Surl.Protocol.Http.UnitTests`).
- [x] `dotnet build Protocols/Http/Surl.Http3.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and coverage stays at 100% line and branch.

## Notes
- Design (decided by Claude under Stewart's delegation; ADR-0182 Amendment 4): `Http3RequestBytesRedactor` in `Surl.Http3.UnitLibrary`, handed out by `Http3ProtocolServer.CreateRedactor`. A `HEADERS` payload carrying `authorization`/`proxy-authorization` (literal or Huffman, found by a fresh QPACK decoder since the table capacity is 0) is written as `****` with its frame header as received; undecodable or chunk-truncated `HEADERS` payloads are withheld (fail closed). Decoded header text (`Decoded` layer) also carried the value in clear, so a chunk starting `:` has those values starred too.
- Limit: the sink makes a fresh redactor per chunk and does not give it the stream, so a chunk is read as frames from its first byte; a body chunk beginning mid-frame that reads as an undecodable `HEADERS` frame is withheld (log detail lost, no secret shown).
- `Documentation/Planning/Decisions` added to `touches` for the ADR-0182 amendment: another Doing task names that folder, but I only appended to ADR-0182, a file no other task is expected to edit.
- Verified: Http3 tests 113 passed, Protocol.Http tests 729 passed, new redactor 100% line and branch.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. HTTP/3 header-block and decoded-text authorization values are starred in decrypted-bytes notes
