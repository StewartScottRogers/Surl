---
id: BL-746
title: Serve a CoAP proxy request that names the listener itself as a local request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-472]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-746 — Serve a CoAP proxy request that names the listener itself as a local request

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

- [ ] Named tests show a `GET` with Proxy-Uri `coap://127.0.0.1:5683/f.txt` to a listener bound there is
      answered `2.05` with `f.txt`'s bytes, and one naming another host, port or scheme is `5.05`.
- [ ] A named test shows Proxy-Scheme `coap` with no Uri-Host, aimed at the listener, is served as local.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

Filed by BL-472, which left this rule of ADR-0097 decision 3 out of its acceptance criteria.

## Log

- 2026-11-05: Created.
