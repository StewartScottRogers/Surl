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
completed: 2026-11-15
---
# BL-1183 — Apply command-stage delay and drop fault rules in the extended DNS server

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching a DNS query makes the DNS server hold the answer, or not answer it (UDP) or cut the connection (TCP, DoT, DoH, DoQ), instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary/DnsQueryFaults.cs` (`FindErrorAsync`, called from the UDP and TCP servers, `DnsOverHttpsHandler.cs` and `DnsOverQuicConnectionServer.cs`) applies only `error` (an RCODE, BL-986). Apply delay and drop at the same point through `CommandStageFaults.FindAndApplyAsync` (ADR-0141) where there is a connection, and for a UDP datagram follow ADR-0124 decision 4: a dropped datagram flow "stops answering" - the query gets no reply.
- Depends on BL-1164 only because both change this library; no functional dependency.
- Judged by RFC 1035 and the DNS transport RFCs (ADR-0083, ADR-0092).

## Acceptance criteria

- [x] Named tests: a `delay` rule holds a UDP query's and a TCP query's answer for `ms` on a fake `TimeProvider` and then answers as usual; one `FaultAppliedEvent` (`Delay`, `Command`) each.
- [x] Named tests: a `drop` rule leaves a UDP query unanswered and aborts a TCP connection with nothing sent; one `FaultAppliedEvent` (`Drop`, `Command`) each.
- [x] Every existing chosen-error test passes unchanged, and a rule for another name changes nothing.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

- `DnsQueryFaults.FindErrorAsync` became `FindAsync`, returning `DnsQueryFaultOutcome` (chosen error or dropped). Delay/drop go through `CommandStageFaults.FindAndApplyAsync`. UDP: dropped query gets no datagram. TCP: connection aborted, nothing written. DoQ: stream reset with RequestCancelled (no connection abort, streams share it). DoH: `HttpAnswer` with `DropsExchange` (HTTP/1 aborts, HTTP/2 resets the stream).
- The old test pinning "a Drop rule is not applied at a query" now uses a RefuseLogin rule, the only behaviour change to an existing test (the drop is now applied by design).
- DnsZoneSet (BL-1164) shows 99% line coverage in this run; not part of this task.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. DNS server applies command-stage delay and drop fault rules on UDP, TCP, DoQ and DoH
