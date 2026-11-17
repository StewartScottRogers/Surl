---
id: BL-1356
title: Cover the CoAP proxy target and refusal branches
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1356 — Cover the CoAP proxy target and refusal branches

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Coap.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.46% branch, 3 failing
  members, branch-only, in `Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary/`:
  - `CoapProxyTarget.ResolveProxyUri(string, ListenUrl)` - `CoapProxyTarget.cs:30`, 93.75% branch; line 39 is 3 of 4
    (`uri.Query.Length < 2 ? [] : ...` - a `Proxy-Uri` with a bare `?` and no query, or with a query, is never seen).
  - `CoapProxyTarget.IsLoopback(string)` - `CoapProxyTarget.cs:66`, 75% branch; line 67 is 3 of 4 (a host that parses as
    an IP address but is not loopback, or one that does not parse, is missing).
  - `CoapProtocolServer.WriteRefusalAsync` (async) - `CoapProtocolServer.cs:93`, 91.67% branch; line 101 is 1 of 2:
    `random ?? CryptographicCoapRandomSource.Instance` only ever takes one side for a non-confirmable refusal.

## Acceptance criteria

- [x] Named `Surl.Extended.Protocol.Coap.UnitTests` tests resolve a `Proxy-Uri` ending in a bare `?` and one with a
      query, asserting the Uri-Query options produced.
- [x] Named tests cover `IsLoopback` with `localhost`, `127.0.0.1`, a non-loopback IP address and a non-IP host name,
      asserting which are served and which refused.
- [x] Named tests write the non-confirmable refusal with and without an injected random source (line 101 both sides),
      asserting the message ID comes from the injected source when given.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Coap.UnitLibrary` reports `100 | 100` with 0 failing
      members, and the fast tests are green.

## Notes

- Tests only (test-writer). Expected bytes from RFC 7252 (extended server, ADR-0083).
- Added the library to `touches`: the remaining branch was the compiler's cached-delegate null check for `Select(Uri.UnescapeDataString)`, which no test can reach reliably, so `ResolveProxyUri` now unescapes through a small loop helper (behaviour unchanged).

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. CoAP library now at 100% line and branch; proxy target and refusal branches tested
