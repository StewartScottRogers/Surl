---
id: BL-482
title: Answer DNS queries over TCP with pipelining
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-481]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-482 — Answer DNS queries over TCP with pipelining

## Goal

The DNS server answers queries over TCP: each message has a two-byte length prefix, several queries share one
connection and may be pipelined, idle connections are closed after the ADR's timeout, and AXFR is served or
refused as the ADR decides.

## Context

- Decision: BL-478's ADR, decisions 4 and 6.
- RFC 1035 section 4.2.2 (the length prefix) and RFC 7766: section 6.2.1 (pipelining; responses may be sent as
  each is ready), section 6.2.3 (idle timeouts), section 8 (a length prefix and message sent together). Answers
  over TCP are never truncated.
- The connection seam: `IConnectionProtocolServer` with `InMemoryConnection`. Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - one query answered with its length prefix;
      - three pipelined queries answered on one connection;
      - an answer larger than 512 bytes sent whole without TC;
      - a length prefix of 0 or over the limit closing the connection;
      - an idle connection closed after the timeout;
      - AXFR answered or refused as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
