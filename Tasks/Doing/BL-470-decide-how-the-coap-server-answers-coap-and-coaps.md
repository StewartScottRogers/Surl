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
completed:
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

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its RFC section, and is listed
      in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Coap.md` exists in the matrix format with every requirement of RFC 7252,
      RFC 7959 and RFC 7641 that binds a server, each "covered by" naming BL-471, BL-472,
      BL-473, BL-474 or BL-475, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
