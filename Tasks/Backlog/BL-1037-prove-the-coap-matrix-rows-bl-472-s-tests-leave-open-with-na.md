---
id: BL-1037
title: Prove the CoAP matrix rows BL-472's tests leave open with named tests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-1037 — Prove the CoAP matrix rows BL-472's tests leave open with named tests

## Goal

Every row of `Documentation/Conformance/Extended/Coap.md` that names BL-1037 names, in its place, a test in
`Surl.Extended.Protocol.Coap.UnitTests` that proves the part of the row BL-472's tests did not.

## Context

- BL-752 traced BL-472's rows to their tests and found these parts no existing test proves:
  - `Coap:RFC7252-4.2-7`: a retransmitted CON request that arrives *after* the client acknowledged the
    server's separate response still gets the Empty ACK from the duplicate cache
    (`CoapFlowSession` remembers it for EXCHANGE_LIFETIME; `CoapFlowSessionTests.ServeAsync_SlowAnswer_*`
    only sends the duplicate before the separate response).
  - `Coap:RFC7252-5.1-1` and the `GET` half of `Coap:RFC7252-5.1-2`: a `GET` leaves the store unchanged and
    two `GET`s answer alike.
  - `Coap:RFC7252-5.4.4-1`: an absent option is read as its default (for example, no Accept serves the
    resource's format; no Block2 is block 0).
  - `Coap:RFC7252-5.5-1`: a payload on a `GET` or `DELETE` is ignored.
  - `Coap:RFC7252-5.10.8-2`: what the server does with If-Match or If-None-Match on a request that ends in
    neither `2.xx` nor `4.12` (the section's MAY); pin whichever the code does and say so in the row.
  - `Coap:RFC7252-6.5-1`: a Uri-Path segment needing percent-encoding with hex letters (such as `ü`, which is
    `%C3%BC`) reaches the content store upper-case.
  - `Coap:RFC6690-4.1-1`: a percent-encoded query filter (`href=/a%20*`) is percent-decoded before it is
    compared.
- Bytes come from RFC 7252 and RFC 6690, never from the Curl port (ADR-0083, ADR-0097). If a test shows the
  server breaks a MUST, fix it in `Surl.Extended.Protocol.Coap.UnitLibrary` under this task.

## Acceptance criteria

- [ ] `Select-String -Path Documentation/Conformance/Extended/Coap.md -Pattern 'BL-1037'` finds nothing.
- [ ] Each of the seven rows above names a test that exists in `Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests`.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes, and the library keeps 100% line and branch coverage.

## Notes

Filed by BL-752.

## Log

- 2026-11-11: Created.
