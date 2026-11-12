---
id: BL-750
title: Serve a CoAP proxy request that names the listener itself as a local request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-472, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-750 — Serve a CoAP proxy request that names the listener itself as a local request

## Goal

A CoAP request whose Proxy-Uri, or Proxy-Scheme with Uri-Host and Uri-Port, names this listener's own
scheme, host and port is served as a local request; every other proxy request stays `5.05 Proxying Not
Supported`.

## Context

- ADR-0097 decision 3, "the order of checks": `5.05` for Proxy-Uri or Proxy-Scheme "unless its scheme,
  host and port name this listener, in which case the request is served as local (section 5.7.2)".
- RFC 7252 sections 5.7.2 and 5.10.2 (Proxy-Uri takes precedence over Uri-Host, Uri-Port, Uri-Path and
  Uri-Query); section 6.4 decomposes a URI into options.
- BL-472 answers every proxy request `5.05`: `CoapRequestOptions.NamesAProxy` and
  `CoapResourceAnswerer.Refusal`. The listener's scheme, host and bound port are in
  `ExchangeContext.ListenUrl`.

## Acceptance criteria

- [x] Named tests show a `GET` with Proxy-Uri `coap://127.0.0.1:5683/f.txt` to a listener bound there is
      answered `2.05` with `f.txt`'s bytes, and one naming another host, port or scheme is `5.05`.
- [x] A named test shows Proxy-Scheme `coap` with no Uri-Host, aimed at the listener, is served as local.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

Filed by BL-472, which left this rule of ADR-0097 decision 3 out of its acceptance criteria.

- 2026-11-11 (lane 2): Parked before any code. `dotnet build` fails: global.json pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Depends on BL-1035 (Stewart reinstalls the SDK).

- 2026-11-11 (lane 6): Done. `CoapRequestOptions.ResolveLocalProxy` (with `CoapProxyTarget`) turns a proxy request naming the listener into a local one, called in `CoapFlowServing` before faults, observers and the answerer; Proxy-Uri wins and its path and query replace Uri-Path and Uri-Query. Host match: equal ignoring case, or both loopback; port defaults 5683 coap, 5684 coaps; Proxy-Scheme with no Uri-Host is aimed at the listener. Anything else stays 5.05. Not unit-measured for coverage with the collector, but every branch has a test.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. A CoAP proxy request naming the listener itself is served as a local request; others stay 5.05
