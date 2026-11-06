---
id: BL-532
title: Route STOMP messages with subscriptions, acknowledgements, receipts, transactions and heart-beats
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-531, BL-664]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-532 — Route STOMP messages with subscriptions, acknowledgements, receipts, transactions and heart-beats

## Goal

The STOMP broker routes SEND frames to subscribers as MESSAGE frames, with the destination semantics BL-530's ADR chose.
It honours the three ack modes, NACK, receipts, transactions and DISCONNECT, and sends and checks heart-beats.

## Context

- Decision: BL-530's ADR, decisions 4 to 7.
- STOMP 1.2: SEND, SUBSCRIBE (`id`, `ack`), UNSUBSCRIBE, ACK and NACK (the `id` header in 1.2), BEGIN, COMMIT
  and ABORT, DISCONNECT, MESSAGE (`destination`, `message-id`, `subscription`, and `ack` when the mode needs one),
  RECEIPT (`receipt-id`), and "Heart-beating".
- Destinations and subscriptions are shared by connections and owned by the server instance. Time from
  `ExchangeContext.TimeProvider`.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR, applied by BL-530's ADR):** `/topic/…` destinations are
  bus topics and `/queue/…` destinations bus queues (BL-631), converted with BL-630's mapping; acknowledgement and
  redelivery are the bus queue's. A message published over `mqtt` reaches a STOMP subscriber and the reverse. The
  library references `Surl.MessageBus.UnitLibrary`. Each SEND and SUBSCRIBE passes the connection's account and
  scheme to BL-664's access check, a refusal answered with an ERROR frame as BL-530's ADR says.

## Acceptance criteria

- [x] Named tests show:
      - SEND delivered as MESSAGE with the specification's headers;
      - `client` ack acknowledging cumulatively and `client-individual` one message at a time;
      - an unacknowledged message redelivered after the subscriber disconnects, as the ADR says;
      - NACK handled as the ADR says;
      - a `receipt` header answered with RECEIPT;
      - a transaction's SENDs delivered only on COMMIT and dropped on ABORT;
      - DISCONNECT with a receipt answered before the close;
      - heart-beat EOLs sent at the negotiated interval;
      - a silent client closed after the ADR's tolerance;
      - a message published on the bus under an MQTT name delivered as MESSAGE on the mapped `/topic/` destination;
      - a SEND and a SUBSCRIBE refused by topic access answered as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Delivered (2026-11-05), all in `StompServerConnectionTests` unless named:
- SEND as MESSAGE: `Send_ToATopic_IsDeliveredAsMessageWithTheSpecificationsHeaders`, `Message_BusHeaders_AreWrittenEscapedButNeverStompsOwnOrOnesHoldingNul`, `Message_In10_LeavesOutHeadersItCannotWriteUnescaped`.
- Ack modes: `Ack_UnderClient_AcknowledgesTheNamedMessageAndEveryEarlierOne`, `Ack_UnderClientIndividual_AcknowledgesTheNamedMessageOnly`.
- Redelivery: `Unacknowledged_AfterTheSubscriberDisconnects_IsRedeliveredWithANewMessageId`, `Unsubscribe_PutsUnacknowledgedQueueMessagesBackAndEndsDelivery`.
- NACK: `Nack_OfAQueueMessage_PutsItBackForRedelivery`, `Nack_UnderClient_IsCumulativeAndATopicMessageIsDropped`.
- Receipts: every `SendAndWaitForReceiptAsync`, e.g. the SEND test above; DISCONNECT with receipt: `ServeAsync_DisconnectWithReceipt_IsAnsweredReceiptAndClosed` and `Unacknowledged_AfterTheSubscriberDisconnects_...`.
- Transactions: `Transaction_SendsAreDeliveredOnlyOnCommitAndDroppedOnAbort`, `Transaction_AcksTakeEffectOnCommitAndAreDiscardedOnAbort`, `Transaction_StillOpenWhenTheConnectionEnds_IsAborted`.
- Heart-beats: `HeartBeat_ClientWantingOne_GetsAnEolEveryIntervalInWhichNothingElseWasWritten`, `HeartBeat_ClientSilentPastTheTolerance_IsClosedWithNoBytes`.
- MQTT name to `/topic/`: `Message_PublishedOnTheBusUnderAnMqttName_IsDeliveredOnTheMappedTopicDestination`.
- Topic access: `Frame_RefusedByTopicAccess_IsAccessRefusedAndCreatesNothing` (SEND and SUBSCRIBE, topic and queue).
- Coverage: `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Stomp.UnitLibrary` reports 100% line, 100% branch, 0 failing members (it also cleared two complexity-12 members BL-531 left, `StompHeaderEscaping.TryDecode` and `StompFrameReader.ReadHeadersAsync`, by extracting a method from each). Fast tests: 17121 passed, 0 failed.

Choices taken where ADR-0118 leaves room (rule 1; no new ADR, since each sits inside its decisions):
- `auto` acknowledges a queue delivery when its MESSAGE is queued in the connection's outbox, not when the transport takes it: the outbox is the server's ordered write path, and a connection that ends before writing puts nothing back that a later `auto` delivery would not also lose.
- A queue's flags are not checked when STOMP declares it (a queue another server declared non-durable is still used), as `Amqp1BusNodeDirectory` does; only `TooManyQueues` refuses there.
- The bus's own `MessageTooLarge` is answered `Frame too large`, the same text as the reader's `--max-message` bound, since decision 8 makes the body limit the smaller of the two.
- A refusal met when COMMIT replays a held SEND is answered ERROR with the held frame's `receipt-id`, the frame at fault.
- The transaction body bound uses `ExchangeLimits.MaxUploadBytes` (`--max-filesize`); 0 means no bound.
- A heart-beat test can only time the server's EOLs on the injected clock, so heart-beats wait on their own token, stopped when the session ends.
- `StompServer` now takes the `MessageBus` in its constructor (as `NatsServer` does); BL-533 composes it.

Found and fixed: the outbox, copied from `NatsOutbox`, marked itself overflowed when anything was queued after `Complete`, which would have sent a false slow-consumer ERROR (`StompOutboxTests.Enqueue_AfterComplete_IsDroppedWithoutOverflowing`). `NatsOutbox` has the same flaw; it is outside this task's `touches`, so it is left for a Nats task.

Filed: BL-863 traces `Documentation/Conformance/Extended/Stomp.md`'s BL-532 rows to these tests; the matrix is under `Documentation/Conformance`, which BL-396 (in Doing) touches.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; destinations are Surl.MessageBus's topics and queues.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The STOMP broker routes SEND to MESSAGE through the shared bus with ack modes, NACK, receipts, transactions and heart-beats
