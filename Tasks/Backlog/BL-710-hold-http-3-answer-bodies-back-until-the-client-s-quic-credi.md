---
id: BL-710
title: Hold HTTP/3 answer bodies back until the client's QUIC credit allows them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-383]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-710 — Hold HTTP/3 answer bodies back until the client's QUIC credit allows them

## Goal

Serving a content-store file over HTTP/3 holds no more than a bounded amount of it in memory:
`Http3DataStream` waits for the client's QUIC credit before it hands over the next piece, as
HTTP/2's `Http2DataStream` waits for its windows.

## Context

- Today `QuicApplicationConnection.WriteAsync` (`Surl.Quic`) queues whatever it is given and
  returns at once ("the rest goes as credit arrives"), so `Http3RequestStreamServer.CopyFileAsync`
  copies a whole file into the stream's unsent buffer however large it is. ADR-0082 Amendment 1,
  item 7, records the gap.
- `QuicStream.UnsentLength` says how much is held back; `Surl.Quic` needs a way to wait until it
  drops below a bound (or until the stream is reset or the connection ends), under the connection's
  one lock, as `ReadAsync` waits for bytes.
- Filed by BL-383.

## Acceptance criteria

- [ ] A named `Surl.Quic.UnitTests` test: a write waits while the stream's unsent bytes are past
      the bound, and completes once the client's `MAX_STREAM_DATA` or `MAX_DATA` lets them go; it
      throws `OperationCanceledException` once the connection ends.
- [ ] A named `Surl.Protocol.Http.UnitTests` test: a file larger than the client's
      `initial_max_stream_data_bidi_local` is sent whole only as the client grants credit, and the
      server never holds more than the bound unsent.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
