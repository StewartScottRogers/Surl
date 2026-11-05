---
id: BL-473
title: Answer CoAP block-wise transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-472]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-473 — Answer CoAP block-wise transfers

## Goal

The CoAP server sends large representations in Block2 blocks of the size the client asks for (or the ADR's
preferred size), with Size2. It receives large uploads in Block1 blocks with `2.31 Continue`, refusing
incomplete or oversized ones as RFC 7959 says.

## Context

- Decision: BL-470's ADR, decisions 4 and 7.
- RFC 7959: section 2.2 (the block option's NUM, M and SZX), 2.3 and 2.4 (Block2 in responses, and the client's
  size preference), 2.5 (Block1, `2.31 Continue`, `4.08 Request Entity Incomplete`, `4.13` with Size1), and 4
  (Size1 and Size2). Section 3's figures are the expected exchanges for tests.

## Acceptance criteria

- [x] Named tests replay RFC 7959 section 3's figures, served from a content-store file of the figure's size: a
      simple Block2 `GET`, a `GET` with an early size negotiation, a Block1 `PUT` with `2.31`, and a `PUT` whose
      block is out of order getting `4.08`. A Block1 total over the limit gets `4.13` with Size1, and a `GET` with
      `Size2: 0` gets Size2 back.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered in the library alone (ADR-0097 decision 4 already decides the behaviour, so no new ADR):
  `CoapBlockOption` (NUM/M/SZX), `CoapBlock2Window` (what a `2.05` carries), `CoapBlock1Uploads`
  (one flow's atomic uploads over `ContentStore.OpenUploadAsync`), `CoapCode.Continue` (2.31) and
  `CoapCode.RequestEntityIncomplete` (4.08); `CoapRequestOptions` now reads Block1, Block2, Size1,
  Size2 and Content-Format; `CoapProtocolServer` gives each flow its uploads and discards them when
  the flow ends.
- Tests: `CoapRfc7959FigureTests` replays Figures 2, 3, 4, 6 and 7, the out-of-order `4.08`, the
  `4.13` for a total and for a Size1 over the limit, and Size2 0. `CoapBlock1UploadsTests`,
  `CoapBlock2WindowTests` and `CoapBlockOptionTests` cover the rest. Coap tests 253 green; library at
  100% line and branch, worst CRAP 10.
- Defaults taken, with why:
  - The preferred SZX is an internal constructor parameter (6, 1024 bytes, by default), so the
    figures replay at their own 128- and 32-byte sizes, and BL-475 can shrink it for DTLS as the
    ADR says.
  - SZX 7 in a request's Block1 or Block2 is `4.00` (RFC 7959 section 2.2 reserves it).
  - A block counts as a retransmission (answered `2.31` again, not written) only when it has M set
    and repeats the last block's offset and length. Anything else at that offset is out of order,
    so it gets `4.08`.
  - A Block1 upload the store will not open (missing directory, hidden path) gets `4.04`, as the
    single-message upload already does for every result except written or too large.
  - Block1 also works for `POST` into a directory: the random name is drawn at block 0, and the
    final `2.01` carries Location-Path and Block1.
  - The fifth upload on a flow gets `4.13` with Size1, which is `--max-filesize` (the ADR's table).
    Uploads that have expired are dropped before the count.
  - A `2.03 Valid` carries no Block2: it has no payload.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The CoAP server sends Block2 blocks with Size2 and receives atomic Block1 uploads with 2.31, 4.08 and 4.13 (RFC 7959)
