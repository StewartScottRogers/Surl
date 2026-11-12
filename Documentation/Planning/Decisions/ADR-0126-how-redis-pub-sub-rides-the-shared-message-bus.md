# ADR-0126 — How Redis pub/sub rides the shared message bus

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-678,
  which built it.
- **Details:** [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 3.8 (Redis pub/sub joins the topic space), on the points it leaves open.

## Context

ADR-0100 decision 3.8 puts Redis `PUBLISH`, `SUBSCRIBE` and `PSUBSCRIBE` on the one
`Surl.MessageBus` the messaging servers share: a channel that is valid UTF-8 without U+0000 is the
canonical topic of the same text, any other channel stays inside the Redis server, a connection
with a pattern holds one bus subscription to `#` and matches each topic against its globs itself,
and `PUBLISH` answers the number of live subscriptions of any protocol the bus delivered to.
BL-491 built pub/sub inside `RedisPubSub`; BL-678 moved it onto the bus and had to decide what
3.8 does not say. The specifications are the Redis "Pub/Sub" page and the `PUBLISH`,
`SUBSCRIBE` and `PSUBSCRIBE` pages, and Redis 7's ACL error texts; the Curl port is never
evidence (ADR-0003).

## Decisions

1. **Which channels are topics.** A channel is a bus topic exactly when its bytes are valid
   UTF-8 and the text is a canonical topic name (`CanonicalTopicName.IsValidTopic`): non-empty, at
   most 65535 bytes, no U+0000, and no `+` or `#`. 3.8 says "verbatim", but a channel `a/+` taken
   verbatim would be a wildcard filter on the bus, and the empty channel and one over 65535 bytes
   are no topic at all; so those stay inside the Redis server with the non-UTF-8 ones. A channel
   is never rewritten to fit (`RedisChannelNames`).
2. **Channel subscriptions.** Each bus-topic channel a connection subscribes to is one bus
   subscription to that topic (the topic is a filter matching only itself). A channel that is not
   a topic, and every pattern, stay in the server's registry, so a local `PUBLISH` still reaches
   matching patterns. The connection's `BusConnection` is made with its first bus subscription and
   disposed with its last, so it carries the login (`AUTH`, `HELLO … AUTH`) the connection had
   when it began subscribing on the bus; anonymous is `null`.
3. **Patterns.** While a connection holds any pattern it holds one bus subscription to `#`.
   Because a canonical filter whose first level is a wildcard never matches a `$` topic (MQTT 4.7.2),
   a `$SYS/…` message reaches channel subscribers but no Redis pattern. Its `pmessage`s are sent from
   the connection's `AfterMessageDelivered`, after its `message`, the order Redis sends them.
4. **`PUBLISH`'s count.** For a bus-topic channel the answer is the bus's
   `DeliveredSubscriptionCount`, with each Redis connection's one `#` subscription replaced by the
   number of its patterns matching the channel (none, one or more), so a Redis client counts as
   Redis counts: each channel subscription and each matching pattern once. A channel that is not a
   topic answers the local count as before. The publish is made for the connection's current login.
5. **Bus refusals.** A publish or subscribe the bus's `--topic-access` grants refuse is Redis 7's
   ACL error `-NOPERM this user has no permissions to access one of the channels used as arguments`;
   a `PSUBSCRIBE` needs `sub` covering `#`. A publish past a bus bound (`MaxMessageBytes`,
   `MaxTopics`, `MaxLogBytes`) is `-OOM command not allowed when used memory > 'maxmemory'.`, the
   reply Redis gives when it cannot take more. A subscribe past the bus's
   `MaxSubscriptionsPerConnection` (1000 by default, below Redis's own 10000) is
   `-ERR max number of subscriptions reached`. A refused `SUBSCRIBE` or `PSUBSCRIBE` leaves
   nothing of that command subscribed, as Redis's ACL check refuses the whole command.
6. **Constructors.** `RedisServer(policy, messageBus)` is the one composition uses, with the
   process's bus. `RedisServer(policy)` stays, on a bus of its own, so a server made alone still
   serves pub/sub among its own connections.

## Consequences

- A Redis client and an MQTT, AMQP, NATS, STOMP or Kafka client of one `surl` process publish to
- `--topic-map` aliases apply to Redis channels (BL-866): `MessageBus.Names` maps `redis` and `rediss` as
  identity, a channel with an alias is the alias target on the bus, and a bus topic comes back to a
  pattern subscriber as the aliased channel name (sent nothing when its name is aliased elsewhere).
  The composition hands the Redis server the shared bus (BL-492).
- Bus messages carry Redis payloads as bytes, QoS 0, never retained; a Redis subscriber sees only
  live fan-out (ADR-0100 decision 3.3).
