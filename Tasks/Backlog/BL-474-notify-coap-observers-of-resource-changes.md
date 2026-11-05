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
completed:
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

- [ ] Named tests show:
      - registration answered with `2.05` and an Observe number;
      - a `PUT` from another flow producing a notification to the observer with a higher number;
      - Max-Age expiry producing a notification;
      - a CON notification unanswered after `MAX_RETRANSMIT` removing the observer;
      - RST removing it;
      - `Observe: 1` removing it;
      - the observer limit refusing a new registration by serving the plain response;
      - a large observed resource notified in its first block, as RFC 7959 section 2.6 says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
