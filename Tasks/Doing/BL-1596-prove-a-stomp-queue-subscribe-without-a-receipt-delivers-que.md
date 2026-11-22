---
id: BL-1596
title: Prove a STOMP queue SUBSCRIBE without a receipt delivers queued and later messages
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1596 — Prove a STOMP queue SUBSCRIBE without a receipt delivers queued and later messages

## Goal

Unit tests pin that a STOMP `SUBSCRIBE` to `/queue/<q>` sent with no `receipt` header gets the messages the queue
already held and the ones sent to it afterwards, so a subscribe that hangs shows up as a failing test rather than
a stuck client.

## Context

Surly cookbook bug report 2026-11-22 (`Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1116-Surl-bug-report.md`,
item 5): one probe's queue `SUBSCRIBE` (the Curl port's client, no `-m`) hung for over 120 s against Surl
`5da5b3eaf`; it was not reproduced. The Curl port's client is no oracle (ADR-0083): this task proves the server
against STOMP 1.2 (https://stomp.github.io/stomp-specification-1.2.html, "SUBSCRIBE", "MESSAGE") with frames written
by hand.

What the code does (no definite cause found while planning):
- `Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary/StompServerConnection.Routing.cs:165-173`
  (`SubscribeToQueue`) creates the bus consumer and parks it in `consumerToStart`; `Answer` (lines 27-46) starts it
  only after the frame's RECEIPT, if any, is queued. Every existing queue test
  (`StompServerConnectionTests.Routing.cs:116-200`) subscribes **with** `receipt`; the no-receipt path and the
  pipelined `CONNECT`+`SUBSCRIBE` in one write are untested.
- An empty queue legitimately delivers nothing until a SEND, and a queue message held unacknowledged by another
  live `ack:client` subscription is not redelivered until that connection ends (ADR-0118 decision 5). Either would
  look like a hang to a client with no time limit; the tests below tell those apart from a server fault.
- `StompTestSession.WaitForAsync` gives up after 30 s (`StompTestSession.cs:17`), so a hang fails the test.

If a test fails, fix the cause in `Surl.Extended.Protocol.Stomp.UnitLibrary` in this task (the failing test is the
reproduction) and say in the Log what it was; if all pass, the Log says the sequence is proven and no cause was found.

Stand-in: the queue is the shared message bus's (ADR-0100 decision 3.3); nothing new is published, seeded or faulted.

## Acceptance criteria

- [ ] `StompServerConnectionTests.Subscribe_ToAQueueHoldingMessagesWithoutAReceipt_DeliversThem` passes: a producer
      session SENDs `m1` and `m2` to `/queue/q` (waiting on its own receipt), then a consumer sends
      `SUBSCRIBE\nid:a\ndestination:/queue/q\n\n\0` and receives both MESSAGE frames in order.
- [ ] `StompServerConnectionTests.Subscribe_ToAnEmptyQueueWithoutAReceipt_DeliversALaterSend` passes: the consumer
      subscribes first, then a producer SENDs `m1`, and the consumer receives it.
- [ ] `StompServerConnectionTests.Subscribe_PipelinedWithConnectInOneWrite_DeliversTheQueuedMessage` passes: `CONNECT`
      (STOMP 1.2, `heart-beat:0,0`) and the no-receipt `SUBSCRIBE` arrive in one write, and CONNECTED is followed by the
      MESSAGE.
- [ ] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary -warnaserror` is clean and
      `dotnet test Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests --filter "TestCategory!=Integration"`
      passes; no test needs `TestCategory=Integration`; the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
