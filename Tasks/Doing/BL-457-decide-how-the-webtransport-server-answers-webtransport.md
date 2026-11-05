---
id: BL-457
title: Decide how the WebTransport server answers webtransport
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-402]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-457 — Decide how the WebTransport server answers webtransport

## Goal

An ADR decides how `surl webtransport://` answers WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16). It
covers session establishment, what the server does with bidirectional streams, unidirectional streams and
datagrams, flow control, session limits, closing and draining, error codes, authentication and limits. A
server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - draft-ietf-webtrans-http3-16 (cited as WTH3): session establishment by extended CONNECT,
    `SETTINGS_WT_MAX_SESSIONS`, stream signals `0x41` and `0x54`, the capsules (`WT_CLOSE_SESSION` 0x2843,
    `WT_DRAIN_SESSION` 0x78ae, and the flow-control capsules), and error codes;
  - the WebTransport overview draft it builds on;
  - RFC 9220, RFC 9297 and RFC 9221.
  Pin the draft revision. The port measured against -16; if a later revision exists, say why -16 is kept or
  replaced.
- Which requests a client sends (what to accept, not a verdict): the port's
  `Curl.Extended.Protocol.WebTransport.UnitLibrary` (the port's ADR-1089) and its matrix `WebTransport.md`. The
  port's `WebTransportReferenceServer` in `Curl.Conformance.Extended.UnitLibrary/WebTransport/Server/` is
  copyable code.
- The HTTP/3 pieces: BL-402's library (BL-410) with extended CONNECT and datagrams (BL-411),
  on `Surl.Quic` (BL-373 to BL-379) and the QUIC seam (BL-380, BL-381). HTTP/3 is answered from the executable
  by BL-384.
- One account set (Stewart, 2026-11-04; BL-622's ADR decision 4): whatever login the session needs reads the
  accounts of `--user`, `--user-file` and the layer-2 options through `Surl.Authentication`'s contracts
  (ADR-0032, ADR-0084 decision 4), never a private credential source; anonymous only under `--allow-anonymous`.

Decide and record:

1. The paths that accept a session, and the response to a CONNECT on another path or with a bad `Origin`.
2. Subprotocol negotiation (the draft's `WT-Available-Protocols` / `WT-Protocol` header fields, or the -16 names).
3. What the server does with data. Planning assumption: echo each bidirectional stream's bytes back on it; answer
   each unidirectional stream with a new unidirectional stream carrying the same bytes; echo each datagram.
4. Flow control: the initial limits and the capsules sent.
5. `SETTINGS_WT_MAX_SESSIONS` and the answer to a session over the limit.
6. Closing: `WT_CLOSE_SESSION` with code and message, `WT_DRAIN_SESSION` on shutdown, and the stream reset
   codes used.
7. Authentication: secure-by-default (a bearer or Basic `authorization` on the CONNECT), with anonymous only
   under `--allow-anonymous`.
8. Each ADR-0006 limit.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 8, each citing the WTH3 section behind it,
      and is listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/WebTransport.md` exists in the matrix format, each "covered by" naming
      BL-458, BL-459 or BL-460, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: logins read the one account set.
- 2026-11-05: Backlog -> Doing.
