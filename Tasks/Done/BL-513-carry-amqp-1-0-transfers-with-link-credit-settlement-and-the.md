---
id: BL-513
title: Carry AMQP 1.0 transfers with link credit, settlement and the message format
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-512, BL-664, BL-706]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-513 — Carry AMQP 1.0 transfers with link credit, settlement and the message format

## Goal

The AMQP 1.0 container grants credit to sending links, accepts transfers (multi-frame included) into its nodes,
and settles them as BL-509's ADR decides. It delivers stored messages to receiving links as their credit
allows, and handles the dispositions the receiver sends.

## Context

- Decision: BL-509's ADR, decisions 5 and 6.
- AMQP 1.0:
  - Part 2, section 2.6.7 (flow and link credit), 2.6.12 and 2.6.13 (transferring messages and settlement), and
    2.7.5 (transfer: `delivery-id`, `delivery-tag`, `more`, `settled`, `aborted`);
  - section 2.7.6 (disposition);
  - Part 3, section 3.2 (message sections: header, delivery-annotations, message-annotations, properties,
    application-properties, data / amqp-sequence / amqp-value, footer) and 3.4 (delivery states).
- Node state is shared by connections and owned by the server instance, not a static.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR, applied by BL-509's ADR):** nodes are the bus's topics
  and queues (BL-631), addresses converted with BL-630's mapping, so a message published over `mqtt` reaches an
  `amqp1` receiver and the reverse. The library references `Surl.MessageBus.UnitLibrary`. The message sections are
  kept byte for byte for an `amqp1` receiver; what other protocols see of them is BL-622's ADR's. Each attach passes
  the connection's account and scheme to BL-664's access check, a refusal answered `amqp:unauthorized-access` as
  BL-509's ADR says.

## Acceptance criteria

- [x] Named tests show:
      - a message sent in three `more` frames stored whole and accepted;
      - a receiving link with credit 2 getting two of three stored messages, then the third after more credit;
      - a pre-settled delivery not redelivered;
      - an unsettled delivery released by the receiver redelivered;
      - a rejected one not redelivered;
      - an aborted transfer discarded;
      - a transfer beyond credit refused with the right condition;
      - the message sections kept byte for byte from sender to receiver;
      - a message published on the bus under an MQTT name delivered to a receiver attached to the mapped address;
      - an attach refused by topic access answered as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- **Plan.** The connection takes the process's `MessageBus` (public constructor) and builds its
  `Amqp1BusNodeDirectory` per connection; an internal constructor takes a directory factory so the
  BL-512 tests keep their recording fake. Frames are answered one at a time under a `SemaphoreSlim`
  gate; bus callbacks only queue on the link (`Amqp1Link.Waiting`) and wake a pump task that sends
  under the same gate, as `Amqp0BrokerConnection`'s outbound channel does. New partial files:
  `.Transfers.cs` (container as receiver), `.Deliveries.cs` (as sender, `flow`, `disposition`).
- **Tests** (414 in the project, 99 new; one BL-512 placeholder test for "frames left to BL-513" removed), the acceptance ones in
  `Amqp1ContainerConnectionTests.Transfers.cs`: `ServeAsync_MessageInThreeMoreFrames_IsStoredWholeAndAccepted`,
  `ServeAsync_ReceiverWithCreditTwo_GetsTwoOfThreeThenTheThirdAfterMoreCredit`,
  `ServeAsync_PreSettledDelivery_IsNotRedelivered`, `ServeAsync_ReleasedDelivery_IsRedeliveredWithItsDeliveryCountRaised`,
  `ServeAsync_RejectedDelivery_IsNotRedelivered`, `ServeAsync_AbortedTransfer_IsDiscarded`,
  `ServeAsync_TransferBeyondCredit_DetachesWithTransferLimitExceeded`,
  `ServeAsync_MessageSections_ReachTheReceiverByteForByteWithoutDeliveryAnnotations`,
  `ServeAsync_MessagePublishedUnderAnMqttName_ReachesTheReceiverOfTheMappedAddress`,
  `ServeAsync_AttachRefusedByTopicAccess_IsAnsweredWithUnauthorizedAccess`. The library is at 100% line
  and 100% branch coverage (measured with the MSTest coverage collector, cobertura).
- **Decisions** (recorded as ADR-0107's "Amendment: how BL-513 reads decisions 5 and 6", decided by
  Claude under Stewart's delegation): a client sender's `flow` sets its `delivery-count` and only
  transfers refill credit, so a flow claiming the limit makes the next transfer
  `amqp:link:transfer-limit-exceeded`; a transfer on a receiving link is `amqp:illegal-state`; frames
  are at most the smaller `max-frame-size`; settling with no outcome releases; `rcv-settle-mode`
  `second` settles once on the client's outcome; `QueueLocked` and `ExclusiveConsumeRefused` refuse the
  attach as `amqp:unauthorized-access`; a queue `reply-to` has no bus field (native form only).
- **Touches.** Added ADR-0107 to record those decisions in it rather than allocate a new ADR number
  while other lanes run; no task in Doing names it. `Documentation/Conformance/Extended/Amqp1.md` (its
  34 `BL-513` rows) is in BL-394's `touches`, so BL-770 was filed to trace them instead.
- **Review** (code-reviewer agent) found five issues, all fixed with tests: an `absolute-expiry-time`
  beyond a `DateTimeOffset` threw and ended the connection (now no expiry); a client gone without
  `close` could let the pump write to a dead transport (`StopWritingAsync` sets `closed` under the gate
  before the pump drains); unsettled topic deliveries were unbounded (a link now holds at most 1000
  unsettled); a delivery larger than the client's session window stalled its link (now written frame by
  frame, `Amqp1OutgoingTransfer`); the dropped-message count was not thread-safe (`Interlocked`). Left as
  is: `drain` while only the session window blocks gives the credit back (Part 2 section 2.6.7 allows
  it), and a message with no body section is accepted as an empty payload.
- **Found on the way:** `cond ? new ReadOnlyMemory<byte>(bytes) : null` types as a non-nullable
  `ReadOnlyMemory<byte>` (null converts through `byte[]`), giving an empty correlation instead of none;
  the code casts to `ReadOnlyMemory<byte>?` and a test pins it.
- **Files touched:** CRLF kept; `dotnet format --verify-no-changes` clean.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; nodes are Surl.MessageBus's topics and queues.
- 2026-11-05: Depends on BL-706 (the bus carries the native form ADR-0107 decision 6 needs), added by BL-509.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. AMQP 1.0 transfers carry messages over the shared bus: link credit, multi-frame and aborted transfers, settlement and dispositions, the message format kept byte for byte; 100% coverage
