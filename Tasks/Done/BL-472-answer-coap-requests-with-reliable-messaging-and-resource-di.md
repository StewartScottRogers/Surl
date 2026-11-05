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
completed: 2026-11-05
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

- [x] Named tests show:
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
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Plan: split the server into a per-flow message layer (`CoapFlowSession`, `CoapDuplicateCache`,
`CoapTransmissionParameters`), the options reader (`CoapRequestOptions`), and the request answering over
the content store (`CoapResourceAnswerer`, `CoapResourceDirectory`, `CoapContentFormat`,
`CoapEntityTag`, `CoapAccessRules`). Randomness through an injected `ICoapRandomSource`, time through
`ExchangeContext.TimeProvider`. The message layer takes the answering as a delegate, so its tests drive
slow answers and retransmission on a manual clock without a store.

Where each acceptance criterion is proved:
- piggybacked `2.05` with token, Content-Format and ETag:
  `CoapProtocolServerTests.ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag`;
- duplicate CON: `ServeAsync_DuplicateCon_IsAnsweredWithTheSameResponseAndNotServedTwice` (a `POST`
  append, so serving twice would show), and the Empty-ACK case in
  `CoapFlowSessionTests.ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain`;
- separate response retransmitted, then given up:
  `CoapFlowSessionTests.ServeAsync_UnacknowledgedSeparateResponse_IsRetransmittedWithDoublingTimeoutsThenGivenUp`
  (timeouts 2, 4, 8, 16, 32 s; 4 retransmissions);
- NON answered with NON: `ServeAsync_NonGet_IsAnsweredWithANon205`;
- `PUT` under uploads and without: `ServeAsync_PutUnderUploads_Gets201ThenChanged204`,
  `ServeAsync_PutWithoutUploads_IsRefusedWith405`;
- `DELETE`: `ServeAsync_Delete_Gets202`; `4.02`: `ServeAsync_UnknownCriticalOption_Gets402NamingIt`;
  `4.04`: `ServeAsync_MissingResource_Gets404WithItsToken`; `4.05`: `ServeAsync_UnsupportedMethod_Gets405`;
  `4.12`: `ServeAsync_PutWithIfNoneMatchOnAnExistingResource_Gets412AndKeepsIt`;
- `/.well-known/core`: `ServeAsync_WellKnownCore_ListsTheResourcesInLinkFormat` and
  `CoapResourceDirectoryTests`.
- Gates: `dotnet build -warnaserror` clean, fast tests green, `Measure-CodeQuality.ps1 -Library
  Surl.Extended.Protocol.Coap.UnitLibrary` reports 100% line, 100% branch, 0 failing members, highest
  complexity 10.

Choices with a sensible default, where ADR-0097 is silent:
- A `PUT` or `POST` the store answers `NotPermitted` after `--allow-uploads` was checked (a hidden path,
  a missing parent directory) is `4.04`: those are the cases ADR-0006 answers as absent. A `DELETE` the
  store answers `NotPermitted` (a read-only share) is `4.05`, the "not permitted" answer.
- A `POST` to a directory checks If-Match and If-None-Match against the file it creates, which does not
  exist yet: If-Match fails (`4.12`), If-None-Match holds.
- A path the store refuses to map (such as a Windows device name) is `4.04` to `GET`, `PUT` and `POST`
  and `2.02` to `DELETE` (section 5.8.4 makes the absent case a success).
- `/.well-known/core` walks at most 16 directories deep, which also ends a cycle of followed symbolic
  links; ADR-0097 bounds the links (1000) but not the walk.
- The first Uri-Query containing `=` is the RFC 6690 filter; the rest are ignored.
- A `GET` whose request ETag matches answers `2.03` only after If-Match, If-None-Match and Accept pass,
  as ADR-0097 decision 3 orders `4.12` and `4.06` before success.
- Every peer over `coap` is unauthenticated (`peerIsAuthenticated: false`); BL-475 passes the DTLS
  answer for `coaps`. `CoapAccessRules` is public so BL-476 can build it from the command line.
- A representation goes whole in one message; BL-473 adds Block2.

Left for follow-up tasks (filed, each depending on this one):
- BL-746: a proxy request naming the listener itself served as local (ADR-0097 decision 3); every
  proxy request is `5.05` for now.
- BL-747: decision 7's requests-in-progress limit, `WriteRefusalAsync`'s `5.03`, and `--max-message`.
- BL-748: trace the requirement matrix `Documentation/Conformance/Extended/Coap.md`, outside this task's
  `touches`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The CoAP server answers GET, PUT, POST and DELETE from the content store with piggybacked or separate retransmitted responses, duplicate detection and /.well-known/core
