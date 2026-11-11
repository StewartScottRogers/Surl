---
id: BL-782
title: Trace the AMQP 1.0 matrix rows BL-513 owns to their named tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-513, BL-1035]
touches: [Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-782 — Trace the AMQP 1.0 matrix rows BL-513 owns to their named tests

## Goal

Every row of `Documentation/Conformance/Extended/Amqp1.md` whose evidence column still reads `BL-513`
names the test in `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests` that shows it, as
ADR-0107's Consequences ask ("each task replaces its ID with the named test").

## Context

- BL-513 built transfers, credit, settlement and the message format but could not edit the matrix:
  `Documentation/Conformance` was in the `touches` of BL-394, in Doing at the time.
- The tests are in `Amqp1ContainerConnectionTests` (its `.Transfers.cs` and `.Settlement.cs` parts),
  `Amqp1BusMessagesTests`, `Amqp1BusNodeDirectoryTests`, `Amqp1DeliveryStateTests` and
  `Amqp1IncomingDeliveryTests`.
- ADR-0107's "Amendment: how BL-513 reads decisions 5 and 6" narrows two rows: a queue `reply-to` has
  no bus field (Surl-Message-1), and settling with no outcome releases a queue delivery.
- `Amqp1:Surl-Storage-1` stays `n/a` (persistence is the bus's).

## Acceptance criteria

- [x] `Select-String -Path Documentation/Conformance/Extended/Amqp1.md -Pattern 'BL-513'` finds no row
      whose evidence column is only `BL-513`; each names one or more tests as `Class.Method`.
- [ ] Each named test exists (`dotnet test --list-tests` on the Amqp1 test project lists it).
- [x] The Surl-Message-1 row states that a `reply-to` naming a queue travels only in the native form.

## Notes

- 2026-11-11 (lane 1): the matrix edit is written - every `BL-513` evidence cell (33 rows, plus the
  "is BL-513's" clauses in Core-2.7.5-1, Core-2.6.6-1, Messaging-3.5.3-1 and Surl-Storage-1) now
  names `Class.Method` tests. The only `BL-513` left in the file is in requirement text citing
  ADR-0107's "Amendment: how BL-513 reads decisions 5 and 6". Every named method was checked against
  the test sources (`public (async Task|void) <Method>(` in `<Class>*.cs`): none missing.
- `dotnet test --list-tests` could not run: global.json pins .NET SDK 10.0.401, which is not installed
  on this machine (installed: 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100), and no built test DLL exists.
  Returned to Backlog behind BL-1035, as BL-960, BL-969 and BL-925 were. The doc edit is left
  uncommitted for the shift to stash; if the stash is lost, redo it from the mapping above (the
  test-to-row choices follow each test's assertions).
- Choice: `Amqp1:Surl-Delivery-2` names `ServeAsync_ModifiedUndeliverableHere_IsNotRedeliveredToTheLinkAndGoesBackWhenItEnds`
  for "go back to the queue when it ends", but no test shows a queue link's 1000-delivery bound
  (only the topic link's). Filed BL-1037 to add that test rather than widen this docs task.
- Choice: `Amqp1:Core-2.6.12-1` names `ServeAsync_ReceiverWithCreditTwo_GetsTwoOfThreeThenTheThirdAfterMoreCredit`,
  which asserts delivery ids 0, 1, 2 on the link's handle; it does not assert tag uniqueness or
  `message-format` 0 on the container's transfers.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Behind BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet test --list-tests cannot check the named tests; matrix edit is done and left for the shift's stash
