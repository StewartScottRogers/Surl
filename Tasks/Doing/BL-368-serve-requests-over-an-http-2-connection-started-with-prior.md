---
id: BL-368
title: Serve requests over an HTTP/2 connection started with prior knowledge
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-365, BL-366, BL-367]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-368 — Serve requests over an HTTP/2 connection started with prior knowledge

## Goal

`surl http://` answers a client that opens with the HTTP/2 connection preface (prior knowledge,
`curl --http2-prior-knowledge`). It serves each request stream through the version-independent
answering code from BL-366, with the streams, flow control and limits that BL-364's ADR decides.

## Context

- Decision: BL-364's ADR, decisions 3, 5, 6 and 7.
- Building blocks: the HPACK library (BL-365, planning name `Surl.Hpack`; add the project reference),
  the frame codec (BL-367), and the answering code (BL-366).
- RFC 9113: section 3.4 (preface), 5.1 (stream states), 5.1.2 (concurrency), 5.2 and 6.9 (flow
  control), 6.5 (`SETTINGS` and its ACK), 6.8 (`GOAWAY`), 8.1 to 8.3 (request and response mapping,
  pseudo-headers, malformed requests).
- The `http` listener tells a preface from an HTTP/1.x request by its first bytes,
  `PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n`. An HTTP/1.x request must still be served exactly as today.
- Each ADR-0006 limit ends with the error code the ADR names. On shutdown the server sends `GOAWAY`
  (ADR-0059: telling a limit from shutdown).
- `surl --aihelp http` must say that HTTP/2 is answered and how. Change `AiHelpProse` (and
  `AiHelpExamples` if the ADR adds an example) in `Surl.Cli.UnitLibrary` in the same diff (root
  `CLAUDE.md`, "`surl --aihelp`").
- Tests replay the bytes BL-364 recorded from `curl --http2-prior-knowledge` through
  `InMemoryConnection`, with no network.

## Acceptance criteria

- [ ] Replaying the recorded prior-knowledge `GET` of a content-store file produces `SETTINGS`, the
      `SETTINGS` ACK, and `HEADERS` and `DATA` frames whose decoded status, fields and body match the
      HTTP/1.1 answer for the same file.
- [ ] Named tests cover: two concurrent streams answered independently; a body larger than the
      client's initial window, sent only as `WINDOW_UPDATE`s allow; a request over the
      concurrent-stream limit refused with the ADR's code; a header block over `--max-message` ending
      with the ADR's error; a malformed request (missing `:path`, uppercase field name) answered with
      `RST_STREAM PROTOCOL_ERROR`; `PING` answered; and `GOAWAY` on shutdown.
- [ ] An HTTP/1.1 request on the same listener is answered exactly as before. Every existing
      `Surl.Protocol.Http.UnitTests` expected byte is unchanged.
- [ ] `surl --aihelp http` describes HTTP/2 prior knowledge, and the `AiHelpTextTests` and
      `CommandLineRunnerAiHelpTests` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
