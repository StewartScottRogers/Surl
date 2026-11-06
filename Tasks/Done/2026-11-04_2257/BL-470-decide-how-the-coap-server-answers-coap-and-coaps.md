---
id: BL-470
title: Decide how the CoAP server answers coap and coaps
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-403]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-470 — Decide how the CoAP server answers coap and coaps

## Goal

An ADR decides how `surl coap://` (UDP) and `surl coaps://` (DTLS 1.2) answer CoAP. It covers the resources
served, methods, options and response codes, message-layer reliability, block-wise transfer, observation,
`/.well-known/core`, the security mode for each scheme, and limits. A server-side requirement matrix traces each
decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - RFC 7252 (CoAP: message format, messaging, request/response semantics, options, response codes, the
    transmission parameters in section 4.8, and `coap`/`coaps` URIs and default ports 5683 and 5684);
  - RFC 7959 (Block1, Block2, Size1, Size2);
  - RFC 7641 (Observe);
  - RFC 6690 (the CoRE link format for `/.well-known/core`);
  - RFC 8132 (FETCH, PATCH and iPATCH), if served.
- Which requests a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Coap.UnitLibrary`
  (the port's ADR-1042, ADR-1047 and ADR-1057) and its matrices `Coap.md` and `Coap-BlockwiseObserve.md`. The
  port's `CoapReferenceServer` is copyable code.
- DTLS: `Surl.Dtls` (BL-403's ADR; BL-412 to BL-415). The UDP seam: `IDatagramProtocolServer`
  and `IDatagramFlow` (ADR-0004), as TFTP uses.

Decide and record:

1. Resources: the content store as the resource tree, `GET` for reads, and `PUT`/`POST`/`DELETE` only under
   `--allow-uploads`. Also `Content-Format` choices and `ETag`/`Max-Age`.
2. The message layer: piggybacked or separate responses, duplicate detection over `EXCHANGE_LIFETIME`, CON
   retransmission of separate responses with RFC 7252's defaults on an injected `TimeProvider`, and RST handling.
3. Unrecognized critical options (`4.02 Bad Option`), `4.05`, `4.04`, `4.13 Request Entity Too Large`, and
   `5.01`.
4. Block-wise: the preferred block size (`SZX`), Block2 for large responses, Block1 for large uploads with
   `2.31 Continue`, `4.08 Request Entity Incomplete`, and Size1/Size2.
5. Observe: which resources are observable, when notifications are sent (a change through `PUT`, and Max-Age
   expiry), CON notifications at least once every 24 hours (RFC 7641 section 4.5), deregistration by RST or
   `Observe: 1`, and the sequence numbers.
6. Security: `coap` is NoSec. Say what that means for writes under secure-by-default (for example, writes only
   over `coaps`, or only with `--allow-uploads`). `coaps` in the modes BL-403's ADR chose, with PSK
   identities from the accounts file.
7. Each ADR-0006 limit per flow (the largest message, exchanges in flight, observers).

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its RFC section, and is listed
      in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/Coap.md` exists in the matrix format with every requirement of RFC 7252,
      RFC 7959 and RFC 7641 that binds a server, each "covered by" naming BL-471, BL-472,
      BL-473, BL-474 or BL-475, or "n/a: <reason>".

## Notes

- 2026-11-04: Decided in
  [ADR-0097](../../Documentation/Planning/Decisions/ADR-0097-how-the-coap-server-answers-coap-and-coaps.md),
  listed in the Decisions `README.md`; matrix
  [`Coap.md`](../../Documentation/Conformance/Extended/Coap.md), 179 rows (RFC 7252 114, RFC
  7959 29, RFC 7641 28, RFC 6690 5, ADR 3). ADR-0006 and ADR-0084 carry "Amended by" lines.
- 2026-11-04: Renumbered on rebase: ADR-0096 to ADR-0097 (BL-378 took ADR-0096 first) and the filed task BL-609 to BL-650 (BL-609..BL-649 were filed first for the Surl.Api and Surl.Mcp hosts).
- Main decisions: two server types (`CoapProtocolServer` for `coap` with a `5.03` refusal writer,
  `SecureCoapProtocolServer` for `coaps` over `Surl.Dtls`) sharing one request path and one
  `CoapObservations` list (planning names); files in the content store are the resources; `GET`,
  `PUT`, `POST` (append to a file, or create a random-named file in a directory with
  Location-Path), `DELETE`; FETCH, PATCH and iPATCH declined (`4.05`); `/.well-known/core` only
  under `--list-directories`; piggybacked within 1 s, else separate CON; RFC 7252 section 4.8
  defaults as constants; block size 1024, stateless Block2, atomic Block1 through
  `ContentStore.OpenUploadAsync`; every file observable, all notifications CON, Max-Age 60 refresh
  catches changes from outside CoAP.
- Defaults taken, and why: `coap` writes need `--allow-uploads` **and** `--allow-anonymous`
  (ADR-0032's "insecure by design" list is closed, and a NoSec write is anonymous); an account
  configured makes `coap` answer `4.01`, as it makes HTTP private; a `coaps` peer is authenticated
  by a PSK identity or a `--cacert` client certificate. Per-flow bounds (128 cached answers, 8
  requests in progress, 4 Block1 sessions, 16 observations; 1000 observations a server) are
  constructor parameters with no new option, as ADR-0093 decision 8 does. Accepted deviations:
  1024-byte blocks despite RFC 7252 section 11.3's amplification SHOULD NOT; the 120-s idle timeout
  is shorter than EXCHANGE_LIFETIME, so a very late duplicate is served again.
- For the planner: the access rules reach the server as a `CoapAccessRules` record and the PSK
  lookup is `Surl.Dtls`'s own (BL-415), both built by BL-476 in `Surl.Console`, so no task needs to
  touch `Surl.Protocol.Abstractions` or `Surl.Authentication`. BL-475 needs `Surl.Dtls`'s session
  to report the peer's PSK identity or verified client certificate and its usable record size
  (BL-414, BL-415 scope). The matrix's command-line rows name BL-476, the composition task, beside
  BL-471 to BL-475. Like ADR-0092 and ADR-0093, no FR row was added after FR-056 in
  `Requirements.md` (ADR-0083 decision 9 asks for one; outside this task's `touches`).
- Filed BL-650 (Surl.Dtls reports the peer's PSK identity, verified certificate and record size) and added it to BL-475's `depends-on`. The matrix's six command-line rows name BL-476 (the composition task that owns default ports), not BL-471 to BL-475: that is where the work lands, as `DnsQuery.md` does.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. ADR-0097 decides how coap and coaps answer; Coap.md matrix traces 179 server requirements
