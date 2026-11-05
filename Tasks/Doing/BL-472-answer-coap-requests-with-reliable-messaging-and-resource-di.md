---
id: BL-472
title: Answer CoAP requests with reliable messaging and resource discovery
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-471]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-472 — Answer CoAP requests with reliable messaging and resource discovery

## Goal

The CoAP server answers `GET`, `PUT`, `POST` and `DELETE` on content-store resources with the right response
codes and options. It acknowledges CON requests (piggybacked, or separate with retransmission), detects
duplicates, and serves `/.well-known/core` in the CoRE link format.

## Context

- Decision: BL-470's ADR, decisions 1, 2, 3 and 6.
- RFC 7252:
  - section 4.2 (CON and ACK), 4.4 (message IDs), 4.5 (duplicates: the same response again), 4.8
    (`ACK_TIMEOUT` 2 s, `ACK_RANDOM_FACTOR` 1.5, `MAX_RETRANSMIT` 4, `EXCHANGE_LIFETIME`);
  - section 5.2 (piggybacked and separate responses), 5.3 (tokens echoed), 5.8 (method definitions), 5.9
    (response codes), 5.10 (options: `Uri-Path`, `Uri-Query`, `Content-Format`, `ETag`, `Max-Age`, `If-Match`,
    `If-None-Match`, `Accept`, `Location-Path`).
  RFC 6690 for the link format.
- Time through an injected `TimeProvider`; the random factor from an injected random source.

## Acceptance criteria

- [ ] Named tests show:
      - a CON `GET` answered with a piggybacked `2.05 Content` carrying the token, `Content-Format` and `ETag`;
      - a duplicate CON answered with the same response and not served twice;
      - a separate response retransmitted until ACKed, then given up after `MAX_RETRANSMIT`;
      - a NON request answered with NON;
      - `PUT` gets `2.04`/`2.01` under uploads and is refused without them;
      - `DELETE` gets `2.02`;
      - an unknown critical option gets `4.02`;
      - a missing resource gets `4.04`;
      - an unsupported method gets `4.05`;
      - a `PUT` with `If-None-Match` on an existing resource gets `4.12`;
      - `/.well-known/core` lists the resources as RFC 6690 says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
