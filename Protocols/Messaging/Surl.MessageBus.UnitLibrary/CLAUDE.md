# Surl.MessageBus.UnitLibrary

The message bus the messaging servers of one `surl` process share (ADR-0100 decision 3):
MQTT 3.1.1 today, and the extended Mqtt5, Amqp0, Amqp1, Kafka, Nats, Stomp and Redis pub/sub
servers as their tasks move onto it. One `MessageBus` is composed per process and handed to
every server of the job; never a static, never a second instance.

What it holds now (BL-629):

- **One topic space.** Topics and filters are canonical, MQTT's names (`CanonicalTopicName`,
  ADR-0100 decision 3.2): levels at `/`, `+` one level, `#` zero or more levels at any level,
  and a wildcard first level never matches a `$` topic. Each server maps its own names onto
  them before calling the bus, and the bus's topics back onto its own names before sending.
- **Name mappings (BL-630).** One static class per protocol, so no broker maps names its own
  way: `MqttTopicNames` (identity, and `$share/<group>/<filter>`), `NatsSubjectNames`
  (`.`, `*`, and a last `>` as `+/#`, one or more levels), `AmqpTopicExchangeNames`
  (`.`, `*`, `#`; keys at most 255 bytes), `KafkaTopicNames` (`[A-Za-z0-9._-]{1,249}`,
  partition 0 only), and `Amqp1AddressNames` and `StompDestinationNames`
  (`/topic/<t>` and `/queue/<q>` as a `BusDestination`). Each returns a
  `TopicNameMapping` or `BusDestinationMapping` whose `TopicNameMappingOutcome` is the typed
  refusal: `InvalidName` inbound, `Unreachable` outbound (a canonical topic the protocol
  cannot name - the server sends nothing), `UnknownDestination`, `UnknownPartition`.
- **Live fan-out.** `MessageBus.Connect` gives a server connection a `BusConnection`;
  `BusConnection.Subscribe` makes a `BusSubscription` whose callback receives every later
  matching `BusMessage`, in publish order, once per subscription, and one member of each
  matching fan-out group round robin. Delivery is serialised and runs outside the bus's lock,
  so a callback may publish or subscribe; it must not block.
- **Retained messages.** One per topic: a `Retain` publish replaces it, an empty retained
  payload removes it, and a subscribe with `includeRetained` is offered the matching ones in
  ordinal order of topic. An expired message is never delivered and is dropped from the
  retained slot when read.
- **Named queues (BL-631).** `BusConnection.DeclareQueue` makes a queue with
  `BusQueueFlags` (durable, exclusive, auto-delete). Declaring it again with other flags is
  `QueueFlagsDiffer`. `BindQueue` and `UnbindQueue` bind it to canonical filters, and a topic
  publish is copied in once per queue, however many filters match.
  `MessageBus.PublishToQueue` sends straight to a queue, as AMQP's default exchange and
  STOMP's `/queue/` do. `Consume` makes a `BusQueueConsumer`, counted against
  `MaxSubscriptionsPerConnection`, that receives nothing until `Start`, so a server's
  consume-ok goes first. Consumers compete round robin among those with room
  (`maxUnacknowledged`, 0 for none). Each `BusQueueDelivery` stays unacknowledged until
  `Acknowledge` drops it. `Reject` with requeue, `Recover`, or the consumer's or connection's
  end puts it back at its own place, marked `Redelivered`; a reject without requeue drops it,
  because the bus has no dead-letter queue. An exclusive queue belongs to its declaring
  connection: any other connection gets `QueueLocked` on declare, bind, consume and delete,
  but may still send to it. It is deleted when its owner goes. An auto-delete queue is
  deleted when its last consumer ends. An expired message is dropped when the queue reaches
  it. Durable is recorded only; persistence is BL-634's.
- **Bounds and outcomes.** `MaxTopics`, `MaxRetainedPayloadBytes`,
  `MaxSubscriptionsPerConnection`, `MaxMessageBytes`, `MaxQueues`, `MaxMessagesPerQueue`
  (unacknowledged included) and `MaxQueuedBytes` (all queues) are constructor parameters
  with `Default…` constants (ADR-0100 decision 3.6). Every refusal a peer can cause is a
  `MessageBusOutcome`, never an exception, and past a bound nothing changes. A full queue
  refuses only its own copy of a topic publish. The publish still succeeds, and
  `BusPublishResult.FullQueueCount` counts the refusals. The queue outcomes that ADR-0100
  does not name yet (`InvalidQueueName`, `UnknownQueue`, `QueueLocked`, `QueueFlagsDiffer`,
  `UnknownDeliveryTag`) follow AMQP 0-9-1's 404, 405 and 406. BL-694 adds them to the ADR.

Not here yet, each its own task: per-topic logs (BL-633), persistence under `<path>/.surl/bus` (BL-634), the MQTT 3.1.1
server's move onto the bus (BL-635), and access checks (BL-664).

This library may reference `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`) and nothing else (ADR-0100 decision
3.1, amending ADR-0002's table); today it references neither. Protocol servers may reference
it. It never touches a socket, and touches the disk only through an injected
`IContentFileSystem`; time comes from the injected `TimeProvider`.
