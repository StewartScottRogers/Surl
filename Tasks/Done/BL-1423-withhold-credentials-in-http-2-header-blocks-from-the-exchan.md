---
id: BL-1423
title: Withhold credentials in HTTP/2 header blocks from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1422]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1423 — Withhold credentials in HTTP/2 header blocks from the exchange log

## Goal

With `--trace` or `--trace-ascii`, an HTTP/2 request's `authorization` and `proxy-authorization` field
values (HPACK, Huffman-coded or literal) never appear in the dump in a recoverable form; they are
written as BL-1417's ADR says.

## Context

- HTTP/2 frames reach the log as raw bytes (`RecordingConnection.cs:32`). HPACK-coded header values,
  Huffman or literal, are recoverable from a `--trace` hex dump, so they are secrets in clear for this
  purpose.
- Finding them needs the connection's HPACK decoding state (`Protocols/Http/Surl.Hpack.UnitLibrary`)
  as the server's own decoder has it. The ADR fixes the approach: decode a copy, or withhold whole
  HEADERS/CONTINUATION payloads that carry such a field.
- `Protocols/Http/Surl.Http2.UnitLibrary` holds the HTTP/2 framing; `Surl.Protocol.Http.UnitLibrary`
  only switches its BL-1422 redactor to this one after the HTTP/2 preface, a one-line change there.

## Acceptance criteria

- [x] An HTTP/2 redactor in `Protocols/Http/Surl.Http2.UnitLibrary`. Fast tests in
      `Protocols/Http/Surl.Http2.UnitTests` show a HEADERS frame carrying `authorization: Basic ...`
      (Huffman-coded, plain literal, and indexed from the dynamic table on a second request) logged in
      the ADR's form, and frames without one unchanged.
- [x] The http server uses it after the prior-knowledge preface and after `Upgrade: h2c`; one named
      test each in `Protocols/Http/Surl.Protocol.Http.UnitTests`.
- [x] Frame headers and lengths in the dump stay as the ADR says (a test pins one dump).
- [x] `dotnet build Protocols/Http/Surl.Http2.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and coverage stays at 100% line and branch.

## Notes

- Four projects only because the wiring is one line in the http server and its two tests; the
  redactor and its own tests are all in `Surl.Http2`.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. HTTP/2 header blocks with authorization/proxy-authorization are withheld from the exchange log; http server switches redactor on preface and h2c 101 (ADR-0182 amendment 3)
