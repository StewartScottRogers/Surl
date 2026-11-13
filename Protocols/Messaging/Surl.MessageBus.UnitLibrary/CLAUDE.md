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
  so a callback may publish or subscribe; it must not block. For MQTT 5 (BL-498):
  `BusConnection.Publish` publishes for the connection's account, marked as its own, and a
  subscription made with `noLocal` is not handed such a message; `BusConnection.AfterMessageDelivered`
  is called once a live message has been handed to all of that connection's subscriptions it
  reached, so a server can send overlapping matches once. BL-721 adds these to ADR-0100.
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
  it. A durable queue that is neither exclusive nor auto-delete is persisted (below).
- **AMQP 0-9-1's queue operations (BL-730, ADR-0112 decisions 5 and 6).**
  `BusConnection.InspectQueue` answers a queue's ready and consumer counts without creating it.
  `PurgeQueue` removes the ready messages, never the unacknowledged ones, and counts them.
  `GetFromQueue` takes one message (`BusGetResult`, with the count still ready). It is held
  unacknowledged for the connection by a hidden get holder, which is never started and never
  counted against `MaxSubscriptionsPerConnection` or as a consumer. `AcknowledgeGet` and
  `RejectGet` settle it by queue name and delivery tag; the connection's end requeues it.
  `DeleteQueue(name, ifUnused, ifEmpty)` refuses with `QueueInUse` (a consumer still taking
  deliveries) or `QueueNotEmpty` (any message, ready or unacknowledged). Otherwise it counts
  every message it deleted. `Consume(..., exclusive: true)` is refused with
  `ExclusiveConsumeRefused` on a queue with consumers, and while it lasts so is every other
  consume. `BusConnection.PublishToConsumedQueues` copies only into bound queues where a
  consumer has room now, and counts the rest in `BusPublishResult.UnconsumedQueueCount`.
  `PublishToQueue(..., onlyWhenConsumed: true)` is `NoReadyConsumer` when none has room.
  `BusQueueConsumer.Stop` ends deliveries but keeps the outstanding ones until they are
  settled or `Dispose` requeues them. A stopped consumer no longer counts as a consumer.
  `BusMessage.HighPriority` puts a message in the queue's high level, handed out before the
  normal level, each level in publish order. `queues/index`'s per-message byte keeps it as
  bit `0x02` beside redelivered `0x01`. Fan-out and logs ignore the level. An auto-delete
  queue goes when its last consumer that is not a get holder ends.
- **Filtered consumers and subscription descriptions (BL-955, ADR-0146 decisions 2.4 and 3.2).**
  `Consume(..., accepts:)` takes a predicate over `BusMessage`: the consumer is offered only what
  it accepts. A message no consumer with room accepts stays ready in its place while later ones
  are still offered, and round robin runs among the consumers that accept each message. The
  predicate is called under the bus's lock, any number of times per message, so it must be pure
  and quick and never call the bus. `PublishToQueue(..., onlyWhenConsumed: true)` and
  `PublishToConsumedQueues` count only a consumer that has room and accepts the message; a get
  ignores predicates. `DeclareQueue(name, flags, subscriptionDescription)` keeps opaque text beside
  the queue, which `InspectQueue` answers in `BusQueueInspectResult.SubscriptionDescription`.
  Declaring again with a non-null description other than the queue's, or on a queue with none,
  is `QueueDescriptionDiffers` and changes nothing; a declare with none checks nothing, so AMQP
  0-9-1 and STOMP never meet it. The description is persisted (`queues/index` is written as
  `SURL-BUS-QUEUES-2`, each queue's name followed by `0x00`, or `0x01` and the description, only
  when some persisted queue has one, so a bus with none writes what earlier builds read),
  snapshotted and restored with its queue, and goes with the queue on reset.
- **Batch publish, all or none (BL-961, ADR-0146 decision 1.6).** `BusConnection.PublishBatch` takes an
  ordered list of `BusBatchEntry` - `ToTopic(message)` or `ToQueue(name, message)` - and answers a
  `BusBatchPublishResult`: `Succeeded` with each entry's `BusPublishResult` in order, or the first refused
  entry's `RefusedIndex` and the outcome its own publish would have met, with nothing published and no
  store change raised. What needs no lock (topic, size, access, the out-of-space fault) is checked first,
  in order, stopping at the first refusal; then, under one hold of the bus's lock, a `BatchPlan` counts each
  entry against the bus as the entries before it would leave it (`MaxTopics`, `MaxLogBytes` after each
  topic's retention, `MaxRetainedPayloadBytes`, `UnknownQueue`, and `MaxMessagesPerQueue` and
  `MaxQueuedBytes` with the batch's own copies), and only when all fit publishes them in order through
  the single publish's own path, so logs, retained slots, queue copies and store changes come out as the
  single publishes would. A bound queue too full for a topic entry's copy refuses only that copy
  (`FullQueueCount`), and the plan names those queues so the publish refuses exactly them. Live and
  queue deliveries run after the lock is let go, so a batch's `Delivered` events follow its other
  changes, each kind in batch order. An expired entry succeeds and is stored nowhere. The fault plan
  may have been asked about an entry after one the locked check later refuses.
- **Topic logs (BL-633).** Every publish the bus takes, retained or not and whichever
  protocol made it, is appended to its topic's `TopicLog` at the next offset, from 0 and
  contiguous; one partition per topic. `BusPublishResult.LogOffset` is the offset it got
  (Kafka Produce's base offset). `MessageBus.ReadLog(topic, offset, maxBytes)` returns
  `BusLogRecord`s in order, as many as fit in `maxBytes` but always the first; an offset at
  the high watermark reads nothing, and one below the log start offset or above the high
  watermark is `OffsetOutOfRange` (Kafka's `OFFSET_OUT_OF_RANGE`). `GetLogOffsets` gives the
  log start offset and high watermark (ListOffsets earliest and latest), `GetLogTopics` every
  topic with a log in ordinal order. A record counts `MessageBus.LogRecordBytes`: payload, native form,
  key, the UTF-8 of header names and values, and 16 bytes for offset and timestamp. Past
  `MaxLogBytesPerTopic` the oldest records go and the log start offset rises, but the
  newest is always kept; past `MaxLogBytes`, after that retention, a publish is `StoreFull`.
  `MaxTopics` counts topics with a log, so a publish on a new topic past it is
  `TooManyTopics`, retained or not (ADR-0100 decision 3.6). An expired message stays in the
  log; one already expired at publish is stored nowhere. BL-697 adds these rules to the ADR.
  `CreateLog(topic)` gives a topic its empty log, as a Kafka Metadata request auto-creates one
  (BL-518, ADR-0105 decision 2), `TooManyTopics` past `MaxTopics`.
- **Bounds and outcomes.** `MaxTopics`, `MaxRetainedPayloadBytes`,
  `MaxSubscriptionsPerConnection`, `MaxMessageBytes`, `MaxQueues`, `MaxMessagesPerQueue`
  (unacknowledged included), `MaxQueuedBytes` (all queues), `MaxLogBytesPerTopic` and
  `MaxLogBytes` (all logs) are constructor parameters
  with `Default…` constants (ADR-0100 decision 3.6). Every refusal a peer can cause is a
  `MessageBusOutcome`, never an exception, and past a bound nothing changes. A full queue
  refuses only its own copy of a topic publish. The publish still succeeds, and
  `BusPublishResult.FullQueueCount` counts the refusals. The five queue outcomes
  (`InvalidQueueName`, `UnknownQueue`, `QueueLocked`, `QueueFlagsDiffer`,
  `UnknownDeliveryTag`) follow AMQP 0-9-1's 404, 405 and 406. ADR-0100 decision 3.6 records
  them, with each queue server's answer.

- **Native forms (BL-706).** `BusMessage.NativeForm` is a `BusNativeForm(Format, Bytes)`: a publisher's own
  encoding of the message, `Format` its lower-case scheme family name (`amqp1` for the AMQP 1.0
  container, ADR-0107 decision 6). The bus copies the bytes at publish and carries them unchanged
  through fan-out, queues (redeliveries included), logs, the retained slot and its files; a server
  reading its own format uses them and every other server ignores them. Its bytes count with the
  payload toward `MaxMessageBytes`, `MaxQueuedBytes` and `LogRecordBytes` (not toward
  `MaxRetainedPayloadBytes`, which stays the payload's). `BusMessageCodec` writes it under flag
  `0x80`, after the key: its format and its bytes. Files written before it never set `0x80` and load
  with none.

- **Persistence (BL-634).** With `--directory <path>`, the composition makes a
  `MessageBusFiles` over `<path>/.surl` and calls `MessageBus.LoadAsync` once, after the
  data-directory lock and before any listener binds; without it nothing touches the disk.
  Under `.surl/bus`: `retained` (rewritten whole), `queues/index` with one file per durable
  queue message in `queues/messages` (written once, deleted when the message leaves), and
  one file per topic log in `logs` (appended to while the log only grows, rewritten whole
  when its start offset rose or a write failed). Every whole write goes through a
  `.<name>-<guid>` file renamed over the old one. `MessageBusFiles`' remarks and
  `BusMessageCodec` give the byte formats, and `MessageBusFilesTests` pins them byte for
  byte. Durable queues that are neither exclusive nor auto-delete persist, with their
  bindings and every message, an unacknowledged one loaded as redelivered. A malformed
  file, or one past a bound, is a `MessageBusLoadException` with the file's path and
  `not a message bus file`, which the composition answers with 37. When `retained` is not
  there, an old MQTT 3.1.1 `.surl/mqtt/retained-messages` is read (its own reason, `not a
  retained-message file`), its messages retained at QoS 0, and `retained` is written at
  once; the old file is never written. A server calls `SaveChangesAsync` after a change;
  a failed write returns `StorageFailed` with the exception message for the server to note,
  the change stays in memory, and the next save writes it. BL-703 adds the formats to
  ADR-0100.

- **Configuration (BL-664).** The constructor's `MessageBusConfiguration` (default `None`)
  carries ADR-0102 decision 3's options, groups already expanded to account names.
  `DeclaredTopic` (`--topic`): a publish on a matching topic is retained if any matching one
  says `retain`, and carried at no more than the lowest QoS ceiling; one without wildcards has
  its empty log from start; an undeclared topic is never refused. `DeclaredQueue` (`--queue`):
  made at start, never exclusive or auto-delete; a client's `DeleteQueue` of one is
  `AccessDenied`; a durable one persists like any durable queue, and one the files hold with
  other flags (persisted durable, now declared not) is made again empty at load. `LoadAsync`
  accepts a bus holding only its still-empty declarations. `MessageBus.Names` is the
  `TopicNameLookup` every server maps through (ADR-0100's planned `ITopicNameLookup`): the
  default mapping for its scheme (`mqtt`, `mqtt5`, `amqp`, `amqp1`, `kafka`, `nats`, `stomp`,
  `redis` and their TLS twins, and `mqtt5+ws`; a Redis channel is canonical as it stands, BL-866), with each `TopicMap` (`--topic-map`) alias of
  one exact name applied both ways; a default name aliased elsewhere is `Unreachable` outbound.
  `TopicAccessGrant`s (`--topic-access`): with none, everyone may do everything; with any, a
  caller needs a grant to its account or `@everyone`. The caller is an account name, `null`
  for anonymous, on `Connect(string?)`, `Publish(BusMessage, string?)` and
  `PublishToQueue(string, BusMessage, string?)`; the parameterless forms are anonymous. A
  publish needs `pub` on a filter matching the topic; a subscribe or a `BindQueue` needs `sub`
  on a filter covering the filter (`CanonicalTopicName.FilterCovers`); a queue is the filter
  `$queue/<name>`, its name one level, `pub` to send to it and `sub` to consume. A refusal is
  `AccessDenied`, checked before anything that would tell whether the topic or queue exists.
  `GetTopicAccess(account, topic)` answers a caller's `pub` and `sub` rights on one topic
  without touching it, as Kafka's Metadata needs (BL-518). ADR-0102 decision 3.5 records these
  choices (BL-704).

- **Events and the out-of-space fault (BL-790, ADR-0124 decisions 1 and 4).** The constructor's
  optional `ISurlEventPublisher` and `IFaultPlan` default to the discarding publisher and the
  empty plan; the composition passes the instance's (BL-803). Every change is a
  `StoreChangedEvent` of `StoreKind.Bus` with `ExchangeId` null (the bus is not told which
  exchange calls, BL-869), named by the canonical topic or by `MessageBus.QueueChangeName`
  (`$queue/<name>`, the access grants' form), `ByteCount` the payload's length:
  `Published` (topic, after its log append - the log append and the publish are one event),
  `Written`/`Deleted` (topic: the retained message set, or cleared or dropped on expiry; `Written`
  with no byte count when `CreateLog` makes an empty log), `Delivered` (topic per live subscription
  handed it; queue per consumer or get), `Enqueued`, `Acknowledged`, `Deleted` with a byte count
  (a reject without requeue, or a purge that removed anything, its bytes in total), and
  `Written`/`Deleted` with no byte count (a queue declared or deleted). Events are published under
  the lock, deliveries from the one delivering thread, so they keep the order the changes had. An
  out-of-space rule is asked for at `FaultStage.StoreWrite`, with the caller's account and the
  path, outside the lock: a topic publish (not expired) is `StoreFull`, a direct `PublishToQueue` to
  an existing queue (not expired) is `QueueFull`, nothing changed, and a `FaultAppliedEvent` names
  the path. Only an answer the plan gives at once counts; a pending one is served as no rule. A topic
  publish (not a seed) also asks, before the lock, for each bound queue's `$queue/<name>`: a queue a
  rule matches refuses only its own copy (`FullQueueCount`). A queue message dropped because it
  expired publishes `Deleted` named for the queue with its payload length, and log records dropped by
  `MaxLogBytesPerTopic` retention publish one `Deleted` named for the topic with their bytes (BL-870).
  Not yet: the fault on a bound queue's copy inside `PublishBatch`.

- **Committed offsets and group access (BL-947, ADR-0145 decisions 4, 6, 9 and 10).** A table of
  `BusCommittedOffset` keyed by Kafka consumer group id and canonical topic, which only the Kafka
  broker uses; it is not a message. `CommitOffset(account, group, topic, offset, leaderEpoch, metadata)`
  keeps the values as given, stamped with the bus's clock, replacing the group's earlier entry for
  the topic. It is `InvalidTopic` for a topic that is not canonical. It is `AccessDenied` without `sub` on
  the topic or on `$group/<id>`, and `StoreFull` when an out-of-space rule matches its change name or
  a new entry would pass `MaxCommittedOffsets` (constructor parameter, default 10000; a replacement
  is always taken). Each commit publishes one `StoreChange.Committed` named
  `MessageBus.CommittedChangeName`: `$group/<id>/<topic>`, with `%` and `/` in the id written `%25`
  and `%2F`. `MayUseGroup(account, group)` is the group check: `sub` on `$group/<id>`, the id one
  level, never matched by a wildcard first level, and every group open with no grants.
  `GetCommittedOffset`, `GetCommittedOffsets(group)` (ordinal by topic) and `GetCommittedGroups`
  (ordinal) read it without checking access. No expiry by time and no seed method. `Reset` empties it.
  `TakeSnapshot` copies it, counting each entry's group, topic and metadata in UTF-8 and 20 bytes,
  and `Restore` puts it back. It is persisted as `.surl/bus/offsets` (`SURL-BUS-OFFSETS-1`, see
  `MessageBusFiles`' remarks) and rewritten whole by `SaveChangesAsync` after a commit.

The original `mqtt` and `mqtts` server keeps its retained messages on this bus (BL-635):
`MqttProtocolServer` takes the run's `MessageBus`, which `Surl.Composition` composes and loads
once per run (`ProtocolServerRegistrations`). That server passes its caller's account to the
bus's access checks (BL-665), as do the extended brokers through their routing tasks.

This library may reference `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`) and nothing else (ADR-0100 decision
3.1, amending ADR-0002's table); it references both. Protocol servers may reference
it. It never touches a socket, and touches the disk only through an injected
`IContentFileSystem`; time comes from the injected `TimeProvider`.
