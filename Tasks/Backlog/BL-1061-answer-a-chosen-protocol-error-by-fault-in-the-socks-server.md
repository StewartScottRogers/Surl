---
id: BL-1061
title: Answer a chosen protocol error by fault in the SOCKS server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: FR-062
created: 2026-11-12
completed:
---
# BL-1061 — Answer a chosen protocol error by fault in the SOCKS server

## Goal

An `error` fault rule naming `socks4`, `socks4a`, `socks5` or `socks5h` (or a listener of one) makes the SOCKS answer the matching request with the chosen error, instead of being refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4 and 9. Reply shape: the SOCKS reply code of the failed CONNECT (SOCKS4 91 to 93, SOCKS5 REP 01 to 08; RFC 1928 section 6), `command` the request command, `path` the target host:port. Measure the exit code of pinned upstream curl for each chosen reply with `Record-CurlExchange.ps1` before pinning a byte (SOCKS is an original protocol, ADR-0003).
- Filed by BL-837's audit: the scheme is registered by `Surl.Composition` but has no row in `FaultReplyShapes` (`Surl.Cli`).
- Consult `ExchangeContext.Faults` per request after login and publish `FaultAppliedEvent` (BL-823's contracts and doubles), as BL-835 did for CoAP, MCP and SignalR.
- Add this family's row to ADR-0124's reply table and its shape to `FaultReplyShapes` (BL-785's `--fault` reply check).

## Acceptance criteria

- [ ] Named tests: a request matching an `error` rule gets exactly the chosen error, a non-matching one is served, and one `FaultAppliedEvent` is published.
- [ ] Named `Surl.Cli` tests: one accepted and one refused `reply` per scheme.
- [ ] ADR-0124's reply table has this family's row.
- [ ] With no rule, every existing test of the touched libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-12: Created.
