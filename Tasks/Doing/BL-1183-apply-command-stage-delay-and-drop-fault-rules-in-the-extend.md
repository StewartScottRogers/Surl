---
id: BL-1183
title: Apply command-stage delay and drop fault rules in the extended DNS server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1164]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1183 — Apply command-stage delay and drop fault rules in the extended DNS server

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching a DNS query makes the DNS server hold the answer, or not answer it (UDP) or cut the connection (TCP, DoT, DoH, DoQ), instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary/DnsQueryFaults.cs` (`FindErrorAsync`, called from the UDP and TCP servers, `DnsOverHttpsHandler.cs` and `DnsOverQuicConnectionServer.cs`) applies only `error` (an RCODE, BL-986). Apply delay and drop at the same point through `CommandStageFaults.FindAndApplyAsync` (ADR-0141) where there is a connection, and for a UDP datagram follow ADR-0124 decision 4: a dropped datagram flow "stops answering" - the query gets no reply.
- Depends on BL-1164 only because both change this library; no functional dependency.
- Judged by RFC 1035 and the DNS transport RFCs (ADR-0083, ADR-0092).

## Acceptance criteria

- [ ] Named tests: a `delay` rule holds a UDP query's and a TCP query's answer for `ms` on a fake `TimeProvider` and then answers as usual; one `FaultAppliedEvent` (`Delay`, `Command`) each.
- [ ] Named tests: a `drop` rule leaves a UDP query unanswered and aborts a TCP connection with nothing sent; one `FaultAppliedEvent` (`Drop`, `Command`) each.
- [ ] Every existing chosen-error test passes unchanged, and a rule for another name changes nothing.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
