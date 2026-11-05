# Amqp0 requirement matrix

The server-side requirements `Surl.Extended.Protocol.Amqp0` answers, for the `amqp` and `amqps`
schemes (ADR-0083 decision 9). Written from AMQP 0-9-1 (2008-11-13,
<https://www.rabbitmq.com/resources/specs/amqp0-9-1.pdf>), its XML protocol definition
`amqp0-9-1.xml`, RabbitMQ's errata for the field-table types, and RabbitMQ's published extensions
for `basic.nack`, `confirm.select` and the server's `basic.cancel`, the specifications
[ADR-0112](../../Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md)
decides by, never from the Curl port's client matrices. Each row binds the broker; "Covered by"
names the task that proves it, until that task replaces its ID with the named test, or says why the
row needs no test.

IDs are `Amqp0:Wire-<section>-<n>` for section 4.2, `Amqp0:<Class>-<method>-<n>` for a rule or
field of the XML's class and method, `Amqp0:Errata-<n>` and `Amqp0:Extension-<n>` for the errata
and RabbitMQ's extensions, and `Amqp0:Surl-<Decision>-<n>` for a rule ADR-0112 adds where the
specification leaves the broker a choice.

## Protocol header and frames (section 4.2; ADR-0112 decisions 1 and 8)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp0:Wire-4.2.2-1 | MUST | The broker reads the client's 8-byte header before writing; `AMQP 0 0 9 1` is answered with `connection.start`. | BL-504 |
| Amqp0:Wire-4.2.2-2 | MUST | Any other header (another version, AMQP 1.0's, not `AMQP`) is answered `AMQP 0 0 9 1` and the connection closed. | BL-504 |
| Amqp0:Surl-Header-1 | MUST | Fewer than 8 header bytes by `--head-timeout` closes the connection with no bytes. | BL-505 |
| Amqp0:Wire-4.2.3-1 | MUST | Frames are read as type, channel, size, payload and the frame-end `0xCE`; a different frame-end is connection exception 501. | BL-504 |
| Amqp0:Wire-4.2.3-2 | MUST | An unknown frame type is connection exception 501. | BL-504 |
| Amqp0:Wire-4.2.3-3 | MUST | A frame above the negotiated frame-max, or above 4096 before `tune-ok`, is refused: 501 after `open-ok`, no bytes before it. | BL-504 |
| Amqp0:Wire-4.2.4-1 | MUST | A method frame's payload is class ID, method ID and the arguments, bits packed into octets; arguments that do not decode are connection exception 502. | BL-504 |
| Amqp0:Wire-4.2.5-1 | MUST | Octets, shorts, longs and long-longs are big-endian; shortstr holds at most 255 bytes, longstr a 32-bit length; timestamps are 64-bit seconds. | BL-504 |
| Amqp0:Errata-1 | MUST | Field-table values are read and written with the errata's type octets `t b B s u I i L l f d D S x A T F V`, nested tables and arrays included. | BL-504 |
| Amqp0:Wire-4.2.6-1 | MUST | A content header frame carries class ID, weight 0, body size, property flags and the properties, and body frames follow until the body size is reached; a body size of 0 has no body frame. | BL-504 |
| Amqp0:Wire-4.2.6-2 | MUST | The broker writes bodies in frames of at most frame-max less 8 bytes. | BL-504 |
| Amqp0:Wire-4.2.7-1 | MUST | A heartbeat frame is type 8 on channel 0 with an empty payload; one on another channel is connection exception 501. | BL-504 |

## The connection class (XML class `connection`; ADR-0112 decisions 2, 3 and 4)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp0:Connection-start-1 | MUST | `connection.start` carries version 0 9, `server-properties` with `product` `surl` and the capabilities table, the mechanisms and the locale `en_US`; no version or platform. | BL-505 |
| Amqp0:Surl-Login-1 | MUST | The mechanisms are the policy's for the scheme and TLS state, `AMQPLAIN` right after `PLAIN`, then `ANONYMOUS` under `--allow-anonymous`; under `--allow-anonymous` `PLAIN` and `AMQPLAIN` are offered on every transport and not checked. | BL-505 |
| Amqp0:Connection-start-ok-1 | MUST | A mechanism not offered closes the connection without any further data (rule `start-ok`/`mechanism`). | BL-505 |
| Amqp0:Surl-Login-2 | MUST | `PLAIN` is RFC 4616's response, an `authzid` other than empty or the `authcid` refused; `AMQPLAIN` is a field table's contents without its size, holding `LOGIN` and `PASSWORD`. | BL-505 |
| Amqp0:Connection-secure-1 | MUST | A challenge mechanism runs as `connection.secure` and `secure-ok` until the policy decides; a final server message is one more `secure`. | BL-505 |
| Amqp0:Surl-Login-3 | MUST | A refused login, after the policy's delay, is `connection.close` 403 with fixed text naming neither account nor cause, then the close of decision 7. | BL-505 |
| Amqp0:Surl-Login-4 | MUST | A frame other than the one due before `open-ok`, or on a channel other than 0, closes the connection with no bytes. | BL-505 |
| Amqp0:Connection-tune-1 | MUST | `connection.tune` offers `channel-max` 256, `frame-max` 131072 or `--max-message` when smaller (never below 4096), and `heartbeat` half of `--idle-timeout` in seconds, 0 without one. | BL-505 |
| Amqp0:Connection-tune-ok-1 | MUST | A `tune-ok` `channel-max` or `frame-max` above the broker's, or a nonzero `frame-max` below 4096, closes the connection without a negotiated close; 0 means the broker's value. | BL-505 |
| Amqp0:Connection-tune-ok-2 | MUST | The client's `heartbeat` is the interval, 0 meaning none. | BL-505 |
| Amqp0:Wire-4.2.7-2 | MUST | With interval `h` the broker sends a heartbeat whenever it has written nothing for `h` seconds, on the injected `TimeProvider`. | BL-505 |
| Amqp0:Wire-4.2.7-3 | MUST | A peer silent for two intervals is closed without `connection.close`. | BL-505 |
| Amqp0:Surl-Idle-1 | MUST | No frame for `--idle-timeout` is `connection.close` 320, heartbeats or not. | BL-505 |
| Amqp0:Connection-open-1 | MUST | `connection.open` of `/` is answered `open-ok`; any other virtual host, the empty one included, is `connection.close` 530 without echoing the name. | BL-505 |
| Amqp0:Connection-close-1 | MUST | A client's `connection.close` is answered `close-ok`; its channels end, outstanding deliveries going back to their queues. | BL-505 |

## Channels and exceptions (XML class `channel`, reply-code constants; ADR-0112 decision 7)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp0:Channel-open-1 | MUST | `channel.open` on a free channel up to the negotiated `channel-max` is answered `open-ok`; on an open channel, or above `channel-max`, connection exception 504. | BL-506 |
| Amqp0:Surl-Channel-1 | MUST | A method on a channel not open is connection exception 504. | BL-505 |
| Amqp0:Channel-close-1 | MUST | A client's `channel.close` is answered `close-ok`, its consumers ended and its outstanding deliveries requeued, marked redelivered. | BL-506 |
| Amqp0:Surl-Exception-1 | MUST | A channel exception is `channel.close` with code, fixed text, class and method; the broker then discards the channel's frames other than `close-ok` and `close`. | BL-506 |
| Amqp0:Surl-Exception-2 | MUST | A connection exception is `connection.close`; the broker discards frames other than `close-ok` and `close` and closes on one of them or after one second. | BL-505 |
| Amqp0:Surl-Exception-3 | MUST | A method only a client receives is 503; a class or method not served (`exchange.bind`, other extensions; `tx` and `confirm` until BL-724) is 540. | BL-505 |
| Amqp0:Surl-Exception-4 | MUST | Every reply text is the code's name and a fixed phrase, never a name, key, account, path or exception text. | BL-505 |
| Amqp0:Channel-flow-1 | MUST | A client's `channel.flow` `active` 0 stops deliveries on that channel and 1 resumes them, answered `flow-ok` with the same bit; the broker never sends `channel.flow`. | BL-506 |

## Exchanges (XML class `exchange`; section 3.1.3; ADR-0112 decision 5)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp0:Exchange-required-instances-1 | MUST | The default exchange, `amq.direct`, `amq.fanout` and `amq.topic` exist from start. | BL-506 |
| Amqp0:Exchange-default-1 | MUST | A publish to the default exchange reaches the queue named by its routing key; the default exchange cannot be declared, deleted or bound (403). | BL-506 |
| Amqp0:Exchange-direct-1 | MUST | A direct exchange routes to the queues bound with a key equal to the routing key. | BL-506 |
| Amqp0:Exchange-fanout-1 | MUST | A fanout exchange routes to every queue bound to it. | BL-506 |
| Amqp0:Surl-Topic-1 | MUST | `amq.topic` is the bus's topic space: a publish is a bus publish on the mapped canonical topic, a binding a bus binding to the mapped filter, so a message published over `mqtt` reaches a queue bound with the mapped key and the reverse. | BL-506 |
| Amqp0:Exchange-topic-1 | SHOULD | A declared topic exchange matches routing keys against binding keys with `*` and `#` inside the broker. | BL-506 |
| Amqp0:Exchange-headers-1 | SHOULD | `amq.headers` and `amq.match` exist, and a headers exchange matches the `headers` table with `x-match` `all` or `any`. | BL-725 |
| Amqp0:Exchange-declare-1 | MUST | A name outside letters, digits, `-`, `_`, `.`, `:` is 406; an absent `amq.` name without `passive` is 403; `passive` and missing is 404. | BL-506 |
| Amqp0:Exchange-declare-2 | MUST | An unsupported type is connection exception 503; a redeclaration with another type is connection exception 530; with another `durable` or `arguments` it is 406. | BL-506 |
| Amqp0:Exchange-delete-1 | MUST | Deleting a missing exchange is 404, a pre-declared one 403, one with bindings under `if-unused` 406; otherwise its bindings go with it. | BL-506 |
| Amqp0:Surl-Exchange-1 | MUST | Durable declared exchanges and their bindings survive a restart under `--directory`. | BL-725 |

## Queues (XML class `queue`; section 3.1.4; ADR-0112 decision 5)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp0:Queue-declare-1 | MUST | An empty name makes a unique server-named queue `amq.gen-` and 22 base64url characters. | BL-506 |
| Amqp0:Queue-declare-2 | MUST | A name the declare would create outside letters, digits, `-`, `_`, `.`, `:` or past 255 bytes is 406; an absent `amq.` name without `passive` is 403. | BL-506 |
| Amqp0:Queue-declare-3 | MUST | `passive` on a missing queue is 404, without creating it. | BL-506 |
| Amqp0:Queue-declare-4 | MUST | A queue redeclared with other `durable`, `exclusive` or `auto-delete` is 406; another open connection's exclusive queue is 405. | BL-506 |
| Amqp0:Queue-declare-5 | MUST | `declare-ok` carries the name, the ready message count and the consumer count. | BL-506 |
| Amqp0:Surl-Queue-1 | MUST | Queues are the bus's: a queue declared here is the one `amqp1`, `stomp` and `--queue` name, and its flags are the bus's. | BL-506 |
| Amqp0:Queue-bind-1 | MUST | `""` names the channel's last declared queue, else 404; a missing queue or exchange is 404; another connection's exclusive queue is 405. | BL-506 |
| Amqp0:Surl-Queue-2 | MUST | A binding on `amq.topic` denied `sub` by topic access is 403; a binding key with no canonical form binds nothing and answers `bind-ok`. | BL-506 |
| Amqp0:Queue-unbind-1 | MUST | Unbinding follows bind's rules; unbinding what is not bound answers `unbind-ok`. | BL-506 |
| Amqp0:Queue-purge-1 | MUST | `queue.purge` removes the ready messages and answers their count; missing is 404. | BL-506 |
| Amqp0:Queue-delete-1 | MUST | `queue.delete` of a missing queue is 404, a `--queue` queue 403, one with consumers under `if-unused` or messages under `if-empty` 406; otherwise `delete-ok` with the count deleted. | BL-506 |
| Amqp0:Extension-1 | MUST | Consumers of a deleted queue end; a client that announced `consumer_cancel_notify` receives `basic.cancel` with `no-wait` set. | BL-506 |
| Amqp0:Surl-Queue-3 | MUST | `no-wait` suppresses the `-ok` and nothing else. | BL-506 |
| Amqp0:Surl-Bus-1 | MUST | The bus inspects a queue without creating it, purges it, gives one message at a time, deletes on condition with the count, holds exclusive consumers, publishes to consumed queues only, stops a consumer keeping its deliveries, and delivers two priority levels. | BL-723 |

## The basic class (XML class `basic`; ADR-0112 decision 6)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp0:Basic-publish-1 | MUST | A publish's method, content header and body frames are joined, a message published to the default exchange in three body frames read back whole by `basic.get`. | BL-506 |
| Amqp0:Surl-Publish-1 | MUST | A method or a content frame out of turn, a header of another class, or body past the body size is connection exception 505. | BL-506 |
| Amqp0:Surl-Publish-2 | MUST | A body size above `--max-filesize` is 311 when the header is read, the body discarded unread into memory. | BL-506 |
| Amqp0:Basic-publish-2 | MUST | A publish to a missing exchange is 404. | BL-506 |
| Amqp0:Basic-publish-3 | SHOULD | An unroutable publish with `mandatory` is returned with `basic.return` 312 `NO_ROUTE` and its content; without it, dropped and noted. | BL-506 |
| Amqp0:Basic-publish-4 | SHOULD | A routed publish with `immediate` reaches only queues with a consumer able to take it, and is returned 313 when none is. | BL-506 |
| Amqp0:Surl-Publish-3 | MUST | A publish denied by topic access is 403; the bus's `StoreFull` and `TooManyTopics` are connection exception 506; a full queue drops a transient message for that queue and closes 506 for a persistent one. | BL-506 |
| Amqp0:Surl-Publish-4 | MUST | `content-type`, string `headers`, `expiration`, `reply-to` and `correlation-id` become the bus message's fields; a bad `expiration` is 406. | BL-506 |
| Amqp0:Surl-Publish-5 | MUST | The exchange, routing key and property bytes are kept as the `amqp0` native form and given back unchanged to an `amqp0` consumer. | BL-506 |
| Amqp0:Basic-06-1 | MUST | Priorities 5 to 9 are delivered before 0 to 4 in a queue, each level in publish order. | BL-506 |
| Amqp0:Basic-delivery-tag-1 | MUST | Delivery tags are per channel, from 1, never 0, one sequence for deliveries and `get-ok`. | BL-506 |
| Amqp0:Basic-consume-1 | MUST | `basic.consume` answers `consume-ok` before the first `basic.deliver`; an empty tag is generated; a tag in use on the channel is connection exception 530. | BL-506 |
| Amqp0:Basic-consume-2 | MUST | `exclusive` on a queue with consumers, or a consumer on an exclusively consumed queue, is 403; a queue denied `sub` by topic access is 403. | BL-506 |
| Amqp0:Basic-deliver-1 | MUST | Consumers receive `basic.deliver` in queue order with `redelivered`, the exchange and routing key from the native form or derived, and the content. | BL-506 |
| Amqp0:Surl-Deliver-1 | MUST | A message from another protocol is delivered with exchange `amq.topic` and the mapped routing key (or `""` and the queue name) and properties built from its fields. | BL-506 |
| Amqp0:Basic-get-1 | MUST | `basic.get` answers `get-ok` with the remaining ready count and the content, or `get-empty` on an empty queue. | BL-506 |
| Amqp0:Basic-09-1 | MUST | `no-ack` deliveries are settled with the bus as they are written. | BL-506 |
| Amqp0:Basic-10-1 | MUST | Explicitly acknowledged deliveries not acknowledged when the channel or connection ends go back to the queue, marked redelivered. | BL-506 |
| Amqp0:Basic-ack-1 | MUST | `basic.ack` settles one tag, `multiple` every tag up to it, 0 with `multiple` all; a tag not outstanding on the channel is 406. | BL-506 |
| Amqp0:Basic-reject-1 | MUST | `basic.reject` with `requeue` puts the message back marked redelivered, without drops it; an unknown tag is 406. | BL-506 |
| Amqp0:Extension-2 | MUST | `basic.nack` rejects with `multiple`, by the same rules. | BL-506 |
| Amqp0:Basic-recover-1 | MUST | `basic.recover` with `requeue` requeues the channel's outstanding deliveries; without it redelivers them to their consumers under new tags with `redelivered`; `recover-ok` answers `recover` only. | BL-506 |
| Amqp0:Basic-cancel-1 | MUST | `basic.cancel` ends deliveries and answers `cancel-ok`, an unknown tag included; outstanding deliveries stay until settled or the channel closes. | BL-506 |
| Amqp0:Basic-qos-1 | MUST | `prefetch-count` and `prefetch-size` bound the channel's (or with `global` the connection's) outstanding deliveries, never stopping a first one, ignored for `no-ack`; 0 means 1000 a consumer. | BL-506 |
| Amqp0:Surl-Storage-1 | MUST | Durable queues, their messages and `amq.topic` bindings persist under `--directory` in `.surl/bus` with each message's native form. | n/a: persistence is the bus's (ADR-0100 decision 3.7, BL-634) and the native form's BL-706's; the broker only hands the bus its messages and bindings, which BL-506's tests show |
| Amqp0:Tx-1 | MUST | `tx.select`, `commit` and `rollback` buffer a channel's publishes and acknowledgements until commit; `commit` or `rollback` on a channel not transacted is 406. | BL-724 |
| Amqp0:Extension-3 | MUST | `confirm.select` puts a channel in confirm mode: each publish is acknowledged with `basic.ack` once the bus holds it, or `basic.nack` when the bus refuses it; mixing with `tx` is 406. | BL-724 |

## Limits and TLS (ADR-0006; ADR-0112 decision 8)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp0:Surl-Limits-1 | MUST | No `open-ok` by `--head-timeout` after accept closes the connection with no bytes. | BL-505 |
| Amqp0:Surl-Limits-2 | MUST | `--max-time` and the connection limits close with `connection.close` 320 once `open-ok` was sent, otherwise with no bytes. | BL-505 |
| Amqp0:Surl-Limits-3 | MUST | Partly read content of one connection past `--max-filesize` is connection exception 506. | BL-506 |
| Amqp0:Surl-Limits-4 | MUST | Past 1000 declared exchanges, 100000 broker bindings, the bus's queue or subscription bounds, the broker answers connection exception 506. | BL-506 |
| Amqp0:Surl-Tls-1 | MUST | `amqps` carries the same protocol inside implicit TLS (TLS 1.2 or 1.3) from the first byte. | n/a: the TLS wrapper is the composition's `ImplicitTlsSchemeServer` (ADR-0010), added by BL-507, not the broker's |
