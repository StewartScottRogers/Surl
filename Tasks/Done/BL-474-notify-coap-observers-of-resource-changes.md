---
id: BL-474
title: Notify CoAP observers of resource changes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-473]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-474 — Notify CoAP observers of resource changes

## Goal

A `GET` with `Observe: 0` registers the client on an observable resource. The CoAP server then sends a
notification with an increasing Observe sequence number whenever the resource changes or its Max-Age lapses, and
removes the observer on RST, `Observe: 1` or a CON notification that is never acknowledged.

## Context

- Decision: BL-470's ADR, decisions 5 and 7.
- RFC 7641: section 3.1 and 4.1 (registration), 4.2 (notifications: the token, the 24-bit sequence number, and
  CON or NON), 4.3.2 and 4.5 (Max-Age freshness, and a CON notification at least every 24 hours), 3.6 and 4.4
  (reordering), 3.6 and 4.5 (deregistration by RST, by an unanswered CON, and by `Observe: 1`), and section 6
  (block-wise with Observe).
- A change comes from a `PUT` or `POST` through this server. Other change sources are as the ADR decides.

## Acceptance criteria

- [x] Named tests show:
      - registration answered with `2.05` and an Observe number;
      - a `PUT` from another flow producing a notification to the observer with a higher number;
      - Max-Age expiry producing a notification;
      - a CON notification unanswered after `MAX_RETRANSMIT` removing the observer;
      - RST removing it;
      - `Observe: 1` removing it;
      - the observer limit refusing a new registration by serving the plain response;
      - a large observed resource notified in its first block, as RFC 7959 section 2.6 says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built as ADR-0097 decisions 5 and 7 say; no new decision needed an ADR.
  - `CoapObservations` (public, shared; at most 1000) and `CoapProtocolServer`'s new public
    constructor taking it, for BL-476 to share between `coap` and `coaps`. The two-argument
    constructor makes its own.
  - `CoapFlowObservers` (one per flow) wraps the answering: it registers on Observe 0, removes on
    Observe 1, and marks a resource changed after a 2.xx answer (not 2.31) to PUT, POST or DELETE.
    `CoapObservation` is one entry. `CoapFlowSession.SendConfirmableAsync` sends every server CON
    (separate responses and notifications) under NSTART 1. It now returns `CoapDelivery`
    (Acknowledged, Reset, GivenUp), so RST and give-up can each remove an observer.
- Choices with a sensible default:
  - Observers of one resource are matched by the content store's mapped `Location`, compared
    ordinally. A change through a differently-cased path on a case-insensitive disk is not
    matched at once. The 60-second Max-Age notification still delivers it, as decision 5 intends
    for changes made outside CoAP.
  - Only a `2.05` registers, as decision 5 says literally. A registration that offers the current
    ETag gets `2.03` without Observe. RFC 7641 section 4.1 allows this ("unable or unwilling").
    `Documentation/Planning/Decisions` is held by BL-563, so the ADR was left as it is.
  - A notification answers a fresh `GET` built from the registration's Uri-Path, Uri-Query and
    Accept, plus Block2 NUM 0 at the registration's size when it sent one. So it is never `2.03`
    and never a later block.
  - The Max-Age timer runs from the end of the last notification's transmission. That is when
    its acknowledgement arrives, at most 60 s plus that time.
  - `CoapFlowSession` no longer disposes its `SemaphoreSlim`. The flow's notifications may still
    be releasing it when the session's serving ends, and a SemaphoreSlim needs no disposal unless
    its wait handle is used.
- Tests: `CoapFlowObserversTests` (16), datagrams through the seam on a manual clock.
  `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Coap.UnitLibrary`: 100% line, 100%
  branch, worst CRAP 10, 0 failing.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. CoAP observers are registered with Observe 0 and notified on PUT/POST/DELETE and Max-Age; RST, give-up, Observe 1 and the limits remove or refuse them
