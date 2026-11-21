---
id: BL-1519
title: Cut an exchange's data connection at a drop fault's after-bytes per BL-1518's ADR-0124 amendment
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1518]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1519 — Cut an exchange's data connection at a drop fault's after-bytes per BL-1518's ADR-0124 amendment

## Goal

An `Accept`-stage `drop;after-bytes=N` rule on an exchange that opens data connections drops it at the byte BL-1518's ADR-0124 amendment counts to, so an FTP download is cut partway through the file.

## Context

Surly cookbook bug report 2026-11-20, item 1. Read BL-1518's amendment to `Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md` first: it decides which bytes count and what is aborted. If it chose a new fault key, the parsing tasks it filed land first; this task is the engine half only.

The code:
- `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:753` creates `ExchangeDataConnections` in `ServeAndLogExchangeAsync`; `ServingEngine.cs:785-789` wraps only the control connection in `DroppingAfterBytesConnection`.
- `Foundation/Surl.Core.UnitLibrary/ExchangeDataConnections.cs:75-80` (`Opened`) wraps each data connection (passive or active) and is where a data connection would join the count.
- `Foundation/Surl.Core.UnitLibrary/DroppingAfterBytesConnection.cs` holds the count per connection today (`sentByteCount`); a count shared by several connections needs a shared counter, guarded, because a server may write the control and data connections from different tasks.

Keep the rest as it is: a rule with no `after-bytes` still aborts before any byte; a datagram flow (`SilencedAfterBytesDatagramFlow`) is not touched; the note and one `FaultAppliedEvent` are written once, however many connections the drop aborts.

Stand-in: no store; the test observes the `FaultAppliedEvent` on the instance's `SurlEventStream` and the bytes each in-memory connection received.

## Acceptance criteria

- [ ] A `ServingEngineTests.Faults.cs` test drives a server that writes a control reply and then a data-connection payload through `ExchangeContext.DataConnections` (as `ServingEngineTests.DataConnections.cs` does) under `drop;after-bytes=N` with N inside the payload, and asserts the data connection received exactly the bytes the amendment allows and was aborted, and the control connection ended as the amendment says.
- [ ] A test that a rule whose N is past everything the exchange sends ends the exchange as usual, data connection included (the twin of `ServeAsync_DropRuleAfterMoreBytesThanTheServerSends_EndsTheExchangeAsUsual`).
- [ ] A test that exactly one `FaultAppliedEvent` (kind `Drop`, stage `Accept`) is published and one drop note is logged when the drop aborts both connections.
- [ ] `ServeAsync_DropRuleAfterBytes_LetsTheServerSendThatManyBytesThenDropsTheConnection` and the other existing `Faults` tests pass unchanged.
- [ ] `dotnet build Foundation/Surl.Core.UnitLibrary -warnaserror` is clean and `dotnet test Foundation/Surl.Core.UnitTests --filter "TestCategory!=Integration"` passes, with 100% line and branch coverage of the changed files; no test needs `TestCategory=Integration`.
- [ ] `Foundation/Surl.Core.UnitLibrary/CLAUDE.md` and the XML comment on `DroppingAfterBytesConnection` state what the drop now counts.

## Notes

The conformance stage proves it with pinned upstream curl 8.21.0: an `ftp://` `RETR` cut by the rule leaves a partial file and the exit code the amendment names, and `-C -` then completes it. If that run is too long for this lane, file it as its own `Integration` task.

## Log

- 2026-11-20: Created.
- 2026-11-21: Backlog -> Doing.
