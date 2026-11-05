---
id: BL-747
title: Apply ADR-0097 decision 7's per-flow CoAP limits and the 5.03 refusal
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-472]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-747 — Apply ADR-0097 decision 7's per-flow CoAP limits and the 5.03 refusal

## Goal

The CoAP server bounds what one client endpoint can make it hold, and answers a refused flow in CoAP's
own words: `5.03 Service Unavailable` with Max-Age 5.

## Context

- ADR-0097 decision 7. BL-472 built the message layer (`CoapFlowSession`) with the duplicate cache's
  bound of 128 Message IDs and NSTART 1, but not:
  - requests in progress per flow, 8: past it a CON gets an ACK with `5.03` and Max-Age 5, a NON is
    ignored;
  - `CoapProtocolServer` implementing `IDatagramRefusalWriter`: `WriteRefusalAsync` answers a first
    datagram that decodes as a CON request with an ACK carrying `5.03` and Max-Age 5, a NON request with
    a NON `5.03`, and anything else with nothing;
  - `--max-message` (`ExchangeContext.Limits`): a CON larger than it is `4.13` with Size1 and, when it
    fits, the Block1 SZX hint of RFC 7959 section 2.9.3; a NON is ignored.
- RFC 7252 sections 4.7 and 5.9.3.4. `CoapCode` has no `ServiceUnavailable` yet (0xA3).

## Acceptance criteria

- [ ] Named tests show the ninth request in progress on one flow answered `5.03` with Max-Age 5, and the
      NON in that place ignored.
- [ ] Named tests show `WriteRefusalAsync` answering a CON request, a NON request and a datagram that is
      no request, each as decision 7's table says.
- [ ] A named test shows a CON over `--max-message` answered `4.13` with Size1.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

Filed by BL-472, which left decision 7's limits beyond the duplicate cache out of its acceptance criteria.

## Log

- 2026-11-05: Created.
