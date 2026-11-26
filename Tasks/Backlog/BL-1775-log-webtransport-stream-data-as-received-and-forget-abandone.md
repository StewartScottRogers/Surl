---
id: BL-1775
title: Log WebTransport stream data as received and forget abandoned HTTP/3 streams in the exchange log redactor
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1762]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1775 — Log WebTransport stream data as received and forget abandoned HTTP/3 streams in the exchange log redactor

## Goal

The WebTransport server's exchange log shows stream data as received, and streams reset or finished part-way through a frame do not make the redactor withhold later requests.

## Context

- Accepted limits of ADR-0182 amendment 21 (BL-1762), found by its code review.
- `Http3RequestBytesRedactor.RedactStream` reads every client bidirectional stream as HTTP/3 frames. A WebTransport
  stream (signal `0x41`, session ID, then data) is therefore misread, and data can be left out of the log.
- A stream that ends part-way through a frame stays remembered; after 4096 of them every later bidirectional chunk of
  the connection is logged as `****`.
- The plain HTTP/3 server must keep reading `0x41` as an unknown frame type, because a `HEADERS` frame may follow it.

## Acceptance criteria

- [ ] A fast test sends a WebTransport bidirectional stream in several chunks and shows its data in the notes as received.
- [ ] A fast test opens more than 4096 such streams on one connection and shows a later request's non-secret frames logged as received.
- [ ] A fast test shows the plain HTTP/3 redactor still withholds a secret `HEADERS` frame that follows a `0x41` frame.
- [ ] An ADR-0182 amendment states the rule, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
