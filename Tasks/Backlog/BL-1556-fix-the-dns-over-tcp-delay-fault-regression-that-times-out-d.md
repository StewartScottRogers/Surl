---
id: BL-1556
title: Fix the DNS-over-TCP delay fault regression that times out DnsQueryDelayDropFaultsTests.ServeAsync_TcpQueryUnderADelayRule_IsAnsweredOnlyAfterTheDelay on every platform
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1556 — Fix the DNS-over-TCP delay fault regression that times out DnsQueryDelayDropFaultsTests.ServeAsync_TcpQueryUnderADelayRule_IsAnsweredOnlyAfterTheDelay on every platform

## Goal

`DnsQueryDelayDropFaultsTests.ServeAsync_TcpQueryUnderADelayRule_IsAnsweredOnlyAfterTheDelay` passes again on
Windows, Linux and macOS, so the shift's pull request #123 (or the one that supersedes it) can merge.

## Context

- PR #123 (shift 20261121-011657), CI run 44904177969: "Build and test" failed on windows-latest, ubuntu-latest and
  macos-latest. The only failing test, three attempts each, is
  `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests/DnsQueryDelayDropFaultsTests.cs:51-62`. It throws
  `TaskCanceledException` after 30 s in `UntilAsync` (`:153`), waiting at `:59` for
  `events.Events.Count == 1 && time.PendingTimerCount == 1`. The delay's timer never appears pending.
- This is a fast test, so a lane's fast test run should have caught it. Check whether the lane that broke it ran the
  DnsQuery tests (and say so in the Log).
- Suspects, all landed this shift: BL-1529 (`efd200fea`, `IConnection.WatchForPeerCloseAsync`; a command-stage
  delay hold ends when the client leaves), BL-1540 (`11c2b1fc2`, reports that the client left during a hold) and
  BL-1521 (`1e01e542c`). The test's `InMemoryConnection` holds only the query bytes, so after the server reads
  them its next read is end of stream. The new peer watch may take that as "the client left" and end the hold at
  once, with no timer, so the condition never holds. Whether that is the test double modelling a half-closed
  client, or production treating a half-close (FIN after the query, which a DNS-over-TCP client may send) as
  leaving, is the question to settle.

## Acceptance criteria

- [ ] The Log names the cause with file:line and the commit that introduced it.
- [ ] If production is wrong (a peer's half-close ends a hold that RFC 7766 lets a DNS client keep waiting through),
      it is fixed so only a reset or full close ends the hold, consistent with ADR-0141 amendment 2, with a named
      fast test pinning a half-closed DNS-over-TCP client still answered after the delay.
- [ ] If only the test double is wrong, `InMemoryConnection` (or this test's use of it) models an open client, and
      no production code changes.
- [ ] Every other test in the solution that hit the same change is found (run the whole fast suite) and fixed the
      same way; the Log lists them.
- [ ] `dotnet build` clean and the full fast suite green (`dotnet test --filter "TestCategory!=Integration"`), not
      only the touched projects.

## Notes

## Log

- 2026-11-21: Created.
