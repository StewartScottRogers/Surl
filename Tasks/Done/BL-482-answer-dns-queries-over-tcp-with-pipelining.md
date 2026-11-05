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
completed: 2026-11-04
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

- [x] Named tests show:
      - one query answered with its length prefix;
      - three pipelined queries answered on one connection;
      - an answer larger than 512 bytes sent whole without TC;
      - a length prefix of 0 or over the limit closing the connection;
      - an idle connection closed after the timeout;
      - AXFR answered or refused as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Tests, all in `DnsTcpProtocolServerTests` unless named: `ServeAsync_OneQuery_AnswersWithItsLengthPrefix`,
  `ServeAsync_ThreePipelinedQueriesInOneRead_AnswersEachInOrderOnOneConnection`,
  `ServeAsync_AnswerOver512Bytes_IsSentWholeWithoutTc`, `ServeAsync_LengthPrefixOfZero_ClosesWithNoBytes`,
  `ServeAsync_LengthPrefixOverMaxMessage_ClosesWithNoBytes`,
  `ServeAsync_IdleConnectionCancelledForALimit_ClosesWithNoMoreBytes`,
  `ServeAsync_MessageNotFinishedWithinTheHeadTimeout_ClosesWithNoBytes`,
  `ServeAsync_ZoneTransferByDefault_IsRefusedWithTheQuestionEchoed`,
  `ServeAsync_ZoneTransferAllowed_SendsTheZoneBetweenTwoSoas`,
  `ServeAsync_ZoneTransferPastOneFrame_SpansFramesWithTheQuestionInTheFirstOnly`, and in `DnsQueryAnswererTests`
  the datagram answers (AXFR `NOTIMP`, IXFR the SOA alone, `NOTAUTH`). Library coverage: 100% line, 100% branch.
- The idle timeout is the engine's, as `ExchangeLimits` says: it cancels the exchange, and the server, seeing
  `IsCancelledForALimit`, notes it and returns with nothing written. The idle test cancels the exchange the way
  the engine does.
- Zone transfer is switched by an `allowZoneTransfer` constructor parameter (default off) on `DnsQueryAnswerer`,
  `DnsUdpProtocolServer` and `DnsTcpProtocolServer`; BL-485 wires `--allow-zone-transfer` to it.
- Defaults taken inside ADR-0092 decision 4, no new ADR needed:
  - the transfer sends records in the zone file's order (the SOA moved to the ends), deterministic for tests;
  - every transfer message carries the OPT when the query had one (RFC 6891 allows it in each; RFC 5936
    section 2.2.5 asks only that EDNS is answered);
  - a transfer question in a class other than IN or ANY is `NOTAUTH`, like a name that is no apex: the server is
    authoritative for no zone in that class;
  - each frame is packed by binary search on the record count with a real write, so compression is counted
    exactly; a lone record too large for any frame is sent alone and cut with TC by `DnsResponseWriter`;
  - the head timeout starts once the first byte of a length prefix arrives, as the ADR says ("once a length
    prefix has begun").
- Refactor: the truncation logic moved from `DnsUdpResponseWriter` to `DnsResponseWriter.WriteWithin`, shared by
  UDP and TCP; UDP behaviour unchanged (its tests pass untouched).
- `ManualTimeProvider` copied into the test project from `Surl.Protocol.Smb.UnitTests` (test projects may not
  reference each other's fakes).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. The DNS server answers over TCP: length-prefixed, pipelined, head and idle timeouts, and AXFR/IXFR refused by default or transferred when allowed
