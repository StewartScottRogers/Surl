---
id: BL-506
title: Answer AMQP 0-9-1 channels, queues, exchanges and the basic class
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-505, BL-664, BL-706, BL-730]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-506 — Answer AMQP 0-9-1 channels, queues, exchanges and the basic class

## Goal

The AMQP 0-9-1 broker opens and closes channels, declares queues and exchanges, binds them, and routes
`basic.publish` (method, content header and body frames) to queues. It answers `basic.get` with `get-ok` or
`get-empty`, returns unroutable mandatory messages with `basic.return`, and serves acknowledgements and consumers
as BL-503's ADR decides. Each misuse is a channel or connection exception with its reply code.

## Context

- Decision: BL-503's ADR, decisions 5, 6, 7 and 8.
- AMQP 0-9-1: section 1.5 (channel), 1.6 (exchange), 1.7 (queue), 1.8 (basic), 3.1.3 (the default exchange binds
  every queue by its name), and 4.2.6 (a content header's body size, and body frames until it is reached). Reply
  codes `404` NOT_FOUND, `405` RESOURCE_LOCKED, `406` PRECONDITION_FAILED and `312` NO_ROUTE (`basic.return`).
- Broker state (queues and exchanges) is shared by connections and owned by the server instance, not a static.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR, applied by BL-503's ADR):** queues are the bus's
  queues (BL-631) and exchange routing maps onto its topic space with BL-630's mapping, so a message published over
  `mqtt` reaches a queue bound on `amq.topic`, and one published here reaches `mqtt` subscribers. The library
  references `Surl.MessageBus.UnitLibrary`. Each publish, declare, bind and consume passes the connection's account
  and scheme to BL-664's access check, a refusal answered `403` ACCESS_REFUSED as BL-503's ADR says.

## Acceptance criteria

- [x] Named tests show:
      - a message published to the default exchange and fetched with `basic.get`, in three body frames;
      - `get-empty` on an empty queue;
      - a mandatory unroutable message returned with `312`;
      - a passive declare of a missing queue gets `404`;
      - re-declaring with different flags gets `406`;
      - an exclusive queue used from another connection gets `405`;
      - `basic.ack` of an unknown delivery tag gets `406`;
      - a server-named queue;
      - consumers delivered in order, if the ADR serves `basic.consume`;
      - a message published on the bus under an MQTT name delivered to a queue bound on `amq.topic` with the mapped
        key;
      - a publish or consume refused by topic access answered as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-05 (BL-503): ADR-0112 decides this task's behaviour. It needs BL-730's bus queue operations (passive inspect, purge, single get, conditional delete, exclusive consumers, immediate publish, consumer stop, two priority levels) and BL-706's native form (`amqp0`). Transactions and publisher confirms are BL-731's, the headers exchange and durable exchanges across restarts BL-732's; the matrix `Documentation/Conformance/Extended/Amqp0.md` lists the rows this task proves.
- 2026-11-05 (BL-506, lane 2): Done. `Amqp0Broker` now takes the process's `MessageBus` (a required constructor argument; BL-507 hands it over) and holds one `Amqp0ExchangeTable` per server. Each connection opens one `BusConnection` for its account after `open-ok`; every frame after that but a heartbeat goes through one ordered outbound queue, so a delivery the bus makes on another connection's thread never overtakes the answer before it (`consume-ok` first). Handlers throw `Amqp0ChannelException` / `Amqp0ConnectionException`; each bus refusal maps through one table, and any outcome it does not name is connection exception 506.
- Acceptance tests, in `Amqp0BrokerConnectionTests`: `Get_PublishedInThreeBodyFrames_IsGetOkWithTheBodyInFrameMaxPieces` (published in three body frames, read back in three frame-max pieces at frame-max 4096), `Get_EmptyQueue_IsGetEmpty`, `Publish_MandatoryUnroutable_IsReturnedWith312`, `QueueDeclare_PassiveMissing_ClosesTheChannelWith404`, `QueueDeclare_OtherFlags_ClosesTheChannelWith406`, `QueueDeclare_ExclusiveQueueOfAnotherConnection_ClosesTheChannelWith405`, `Ack_UnknownDeliveryTag_ClosesTheChannelWith406`, `QueueDeclare_EmptyName_IsAServerNamedQueueTheChannelThenNamesByEmpty`, `Consume_ThreeMessages_AreDeliveredInPublishOrderAfterConsumeOk`, `Get_MessagePublishedOnTheBusUnderAnMqttName_ReachesTheQueueBoundOnAmqTopicWithTheMappedKey`, `Publish_RefusedByTopicAccess_ClosesTheChannelWith403`, `Consume_RefusedByTopicAccess_ClosesTheChannelWith403`. 275 tests in the project; the library measured at 100% line and 100% branch coverage with the MSTest coverage collector.
- Choices (defaults, within ADR-0112): an unroutable `immediate` publish is returned 312 even without `mandatory`, as decision 6 says of a publish both unroutable and immediate. A message routed to a queue gets the canonical form of its routing key as its bus topic, or the key itself when it has none (the bus does not check a queue message's topic). `headers` entries that are not long strings are dropped without a log note. `channel.open`'s reserved field and `queue.declare`'s `arguments` are read and not checked.
- Left for follow-up, filed: BL-754 (`basic.qos` bounds across a channel's consumers and with `global`, `prefetch-size`, and `recover` without `requeue` back to the original consumer; here `prefetch-count` bounds each consumer the channel makes afterwards and `recover` always requeues) and BL-755 (`basic.cancel` to the consumers of a deleted queue for clients that announced `consumer_cancel_notify`).
- Touches: added `Documentation/Conformance/Extended/Amqp0.md` (its rows named BL-506 as the task to replace them with tests); BL-625, the only other task in Doing, touches only `Application/Surl.Mcp*` and `Surl.slnx`.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; queues and routing are Surl.MessageBus's.
- 2026-11-05: Waits on BL-706 and BL-730 too (BL-503, ADR-0112).
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The AMQP 0-9-1 broker opens channels, declares exchanges and queues on the shared bus, routes basic.publish, and answers get, consume, ack, reject, nack and recover, each misuse with its reply code
