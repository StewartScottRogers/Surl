# ADR-0112 — How the AMQP 0-9-1 broker answers `amqp` and `amqps`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-503. BL-504 builds the protocol header and the frame, method and field-table codec
  (decisions 1 and 8's frame limits), BL-505 the connection class, authentication, tuning,
  heartbeats and the virtual host (decisions 2, 3, 4 and 7), BL-506 channels, exchanges, queues
  and the basic class (decisions 5, 6 and 7), and BL-507 the command line and the composition
  (decision 8). BL-730 gives `Surl.MessageBus` the queue operations decisions 5 and 6 need,
  BL-731 serves transactions and publisher confirms (decision 6), and BL-732 the headers exchange
  and durable exchanges across restarts (decision 5).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof),
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decision 4 (AMQP 0-9-1 logs in through `ISaslAuthenticationPolicy`),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  (queues are the shared message bus's queues, `amq.topic` is its topic space, and the accounts
  are the shared account set) and
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decisions 3.2 and 3.4 (declared queues and topic access).
- **Amends:** ADR-0100 decision 3.6's AMQP 0-9-1 column: `506 resource-error` is a connection
  exception, not a channel one (decision 7). ADR-0100 decision 3.3's named queue: it delivers in
  two priority levels, not plain FIFO (decision 6, BL-730).

## Context

`Surl.Extended.Protocol.Amqp0` (in `Protocols/Messaging/`, ADR-0083's table) answers the `amqp`
and `amqps` schemes the Curl port adds, on ports 5672 and 5671 (ADR-0083 decision 7). By ADR-0083
decision 1 it is judged by its specification, not by upstream curl, which has no AMQP, and not by
the Curl port. The specification read, and pinned, is **AMQP 0-9-1, 2008-11-13**
(<https://www.rabbitmq.com/resources/specs/amqp0-9-1.pdf>; below "section n") with its XML protocol
definition **`amqp0-9-1.xml`** (<https://www.rabbitmq.com/resources/specs/amqp0-9-1.xml>; below
"the XML"), which carries the class and method IDs, the fields, the reply-code constants with their
`soft-error` (channel) and `hard-error` (connection) classes, and the named rules cited below as
"rule `<method>`/`<name>`":

- section 3.1 (the server's functional model: virtual hosts, exchanges 3.1.3, queues 3.1.4);
- section 4.2 (wire format: 4.2.2 protocol header, 4.2.3 frames and the frame-end octet `0xCE`,
  4.2.5 data types and field tables, 4.2.6 content frames, 4.2.7 heartbeat frames);
- the XML's classes `connection` (10), `channel` (20), `exchange` (40), `queue` (50), `basic` (60)
  and `tx` (90), and its constants (`frame-min-size` 4096, the reply codes 200 to 541).

RabbitMQ's errata (<https://www.rabbitmq.com/amqp-0-9-1-errata.html>) settle the field-table type
octets (`t b B s u I i L l f d D S x A T F V`), where the PDF and the XML disagree. Three methods
the 2008 specification lacks are served from RabbitMQ's published protocol extensions
(<https://www.rabbitmq.com/docs/specification>, its `amqp0-9-1.extended.xml`), because 0-9-1
clients send them and announce them in `connection.start`: `basic.nack` (60.120),
`confirm.select`/`select-ok` (85.10/85.11), and the server's `basic.cancel` to a consumer whose
queue was deleted. Every other RabbitMQ extension (`exchange.bind`, `connection.blocked`,
queue arguments `x-*`, consumer priorities) is not served (decision 7).

The port's `Curl.Extended.Protocol.Amqp0.UnitLibrary` and its matrices `Amqp0.md` and
`Amqp0-Messaging.md` were read only to learn what a client sends, so that the broker accepts it:
the header `AMQP 0 0 9 1`; `connection.start-ok` with `PLAIN` always (it gives up when `PLAIN` is
not offered), `guest`/`guest` when no credential is given, locale `en_US` and the capability
`authentication_failure_close`; `tune-ok` with the smaller of the server's frame-max and 131072,
heartbeat 0; `connection.open` of the URL's first path segment, `/` when empty; channel 1;
`queue.declare` of the URL's queue with no flags; then `basic.publish` to the default exchange
with no properties, or `basic.get` with `no-ack`; then `channel.close` and `connection.close`.
Nothing below is taken from the port's output, and no expected byte comes from it.

## Decision

### 0. What the broker is

- **One AMQP 0-9-1 broker on the shared bus.** Its queues are ADR-0100's `Surl.MessageBus` named
  queues, `amq.topic` is the bus's topic space, and its logins are the shared account set, handed
  to it by the composition (BL-507). A message published over `mqtt` reaches a queue bound on
  `amq.topic` with the mapped key, and one published to `amq.topic` reaches `mqtt` subscribers. The
  library references `Surl.Protocol.Abstractions` and `Surl.MessageBus` (and `Surl.Content` for
  BL-732's file), never another protocol server.
- **State of its own, per server instance** (never a static): the exchanges other than
  `amq.topic`, with their bindings. **Per connection:** the negotiated tuning, the channels, and on
  each channel its consumers, unacknowledged deliveries, quality of service, flow state and the
  partly read content. Nothing of a connection outlives it.
- The broker only answers: it never opens a channel, and the only methods it sends unasked are
  `basic.deliver`, `basic.return`, `basic.cancel` (decision 5), heartbeats, and the `close`
  methods of decision 7.

### 1. The protocol header (section 4.2.2)

- The client's first 8 bytes are read before anything is written. `A M Q P 0x00 0x00 0x09 0x01`
  is accepted, and the broker answers with `connection.start` (decision 2).
- **Any other 8 bytes** - another AMQP version (`AMQP 1 1 0 9` for 0-9, `AMQP 1 1 9 1` for 0-8),
  AMQP 1.0's `AMQP 0 1 0 0` or `AMQP 3 1 0 0`, or not `AMQP` at all - are answered with
  **`AMQP 0 0 9 1`** and the connection is closed, as section 4.2.2 says. AMQP 1.0 has its own
  scheme, `amqp1` (ADR-0107); it is not negotiated here.
- Fewer than 8 bytes by `--head-timeout` closes the connection with no bytes.
- There is no in-band TLS: AMQP 0-9-1 has no TLS header. TLS is `amqps` (decision 8).

### 2. `connection.start`, logins and refusals (XML class `connection`; RFC 4616; RFC 4505)

- **`connection.start`** (10.10) carries `version-major` 0, `version-minor` 9, and:

  | Field | Value | Why |
  | --- | --- | --- |
  | `server-properties` | `product` = `surl` (longstr), and `capabilities` = a table of `basic.nack`, `consumer_cancel_notify` and `authentication_failure_close`, and `publisher_confirms` once BL-731 serves confirms, each `true` (`t`) | no version or platform is sent (ADR-0006 section 3); the capabilities name exactly the extensions served (Context) |
  | `mechanisms` | the mechanisms `ISaslAuthenticationPolicy.GetSaslMechanisms` returns for the scheme and TLS state, in the policy's order, `AMQPLAIN` right after `PLAIN` whenever `PLAIN` is listed, then `ANONYMOUS` under `--allow-anonymous`; separated by single spaces | ADR-0084 decision 4; `AMQPLAIN` is `PLAIN`'s credentials in a field table |
  | `locales` | `en_US` | reply texts are English |

  The policy withholds `PLAIN` and the other plain-text mechanisms on a connection without TLS
  unless `--allow-plaintext-auth` (FR-028) is given, so by default `amqp://` offers only the
  challenge mechanisms and `amqps://` offers `PLAIN` and `AMQPLAIN` too. **Under
  `--allow-anonymous`, `PLAIN` and `AMQPLAIN` are offered on every transport and their credentials
  are not checked**: 0-9-1 clients log in with `PLAIN` and almost none speaks `ANONYMOUS` (the port's
  gives up without `PLAIN`), so an anonymous broker that withheld `PLAIN` would refuse the clients
  it was loosened for. This differs from ADR-0107's AMQP 1.0 rule on purpose; the start-up warning
  ADR-0032 prints for `--allow-anonymous` stands.
- **`connection.start-ok`** (10.11): `client-properties` is read and kept only for the exchange
  log; `locale` is read and ignored (the replies are English whatever it says).
  - **A mechanism not offered** closes the connection **with no bytes**, as rule
    `start-ok`/`mechanism` requires ("closing the connection without sending any further data").
  - **`PLAIN`**: the response is RFC 4616's `authzid NUL authcid NUL passwd`; an `authzid` neither
    empty nor the `authcid` is refused (Surl grants no proxy rights).
  - **`AMQPLAIN`**: the response is a field table's contents **without** its leading 4-byte size,
    as RabbitMQ's clients write it, holding `LOGIN` and `PASSWORD` as longstr (`S`) values; any
    other shape is refused like a wrong password.
  - **`ANONYMOUS`** (only under `--allow-anonymous`): accepted at once, the trace not checked.
  - **Every other mechanism** runs through `ISaslAuthenticationPolicy.StartSaslExchange`: each
    challenge is a `connection.secure` (10.20), each answer a `connection.secure-ok` (10.21), until
    the policy decides. A mechanism whose last step is a server message (SCRAM's server-final)
    sends it as one more `connection.secure`, and the client's `secure-ok` to it, whatever it holds,
    ends the exchange.
- **Accepted:** the broker sends `connection.tune` (decision 3). The account logged in, or none
  for `ANONYMOUS` and under `--allow-anonymous`, is the caller ADR-0102's topic access judges.
- **Refused:** after the policy's one-second delay (ADR-0032 section 8), the broker sends
  **`connection.close` with `403` `ACCESS_REFUSED`**, reply text `ACCESS_REFUSED - login refused`,
  class 10 and method 11 (or 21 after a `secure-ok`), then waits as decision 7 says for
  `close-ok` and closes. The text never names the account or the cause (ADR-0032 section 5). Every
  client implements `connection.close` (the XML's chassis: client MUST), so a negotiated close is
  always understood, and it tells the client why where closing the socket would not; it is sent
  whether or not the client announced `authentication_failure_close`.
- **A frame other than `start-ok` or `secure-ok`** where one is due, or any frame on a channel
  other than 0 before `connection.open-ok`, closes the connection with no bytes: tuning has not
  happened, so no `close` can be negotiated.

### 3. `connection.tune`, frame-max and heartbeats (XML `connection.tune`/`tune-ok`; section 4.2.7)

- **`connection.tune`** (10.30):

  | Field | Value | Why |
  | --- | --- | --- |
  | `channel-max` | **256** | decision 8's channel bound |
  | `frame-max` | **131072**, or `--max-message` when smaller, never below 4096 (`frame-min-size`) | a frame is read whole before it is decoded, so the frame is the read bound ADR-0006's `--max-message` sets; 131072 is what common 0-9-1 brokers and the port's client offer |
  | `heartbeat` | **60** seconds: half of `--idle-timeout` (120 s) in whole seconds, at most 65535; **0** when the idle timeout is off | half the idle timeout, so a heartbeating client is never closed as idle |

- **`connection.tune-ok`** (10.31):
  - `channel-max` 0 means the client sets no limit, and the broker's 256 holds; a value above 256
    closes the connection **with no bytes** (rule text of `tune-ok`'s `channel-max`: "the server
    MUST close the connection without attempting a negotiated close").
  - `frame-max` 0 means the broker's value holds; a value above the broker's, or a nonzero value
    below 4096, closes the connection with no bytes (the same text for `frame-max`, and its
    `frame-min-size` floor). Otherwise the client's value is the connection's frame-max both ways.
  - `heartbeat` is taken as the client sends it, 0 meaning no heartbeats: the field "the client
    wants" decides, and the XML sets no ceiling.
  - Until `tune-ok`, every frame may be up to 4096 bytes and no larger (`frame-min-size`).
- **Heartbeats** (section 4.2.7), with a negotiated nonzero interval `h`:
  - the broker writes a heartbeat frame (type 8, channel 0, empty payload, `0xCE`) whenever it has
    written nothing for `h` seconds;
  - when **nothing at all** has arrived for `2 * h` seconds, the broker closes the connection
    **without** `connection.close`, as section 4.2.7 says of a peer silent for two intervals;
  - a heartbeat frame on any channel other than 0 is a connection exception **`501`
    `FRAME_ERROR`** (section 4.2.7).
- **`--idle-timeout`** applies as well, with or without heartbeats: when no frame of any kind (a
  heartbeat counts) arrives for that long, the broker sends `connection.close` `320`
  `CONNECTION_FORCED`, text `CONNECTION_FORCED - idle timeout`, and closes as decision 7 says.
- Every timer runs on `ExchangeContext.TimeProvider`; never `Thread.Sleep` or polling.

### 4. Virtual hosts (section 3.1.2; XML `connection.open`)

- **One virtual host, `/`.** `connection.open` (10.40) naming `/` is answered `open-ok` (10.41,
  `reserved-1` empty); its reserved `capabilities` and `insist` fields are read and ignored.
- **Any other name, the empty one included**, is answered **`connection.close` `530`
  `NOT_ALLOWED`**, text `NOT_ALLOWED - unknown virtual host`, class 10, method 40. The text never
  echoes the name.
- Why one: the XML's `virtual-host` field says a server with several "MUST enforce a full
  separation of exchanges, queues, and all associated entities per virtual host". A second virtual
  host would need a second bus, and then a message published over `mqtt` could reach only one of
  them, which is the opposite of ADR-0100's one shared topic space. Every account that logs in may
  open `/`; access within it is ADR-0102's.

### 5. Exchanges and queues (sections 3.1.3 and 3.1.4; XML classes `exchange` and `queue`)

**Exchange types and the pre-declared exchanges** (rules `exchange`/`required-types`,
`recommended-types`, `required-instances`, `default-exchange`):

| Exchange | Type | Routes to |
| --- | --- | --- |
| the default exchange, name `""` | direct | the bus queue whose name is the routing key (every queue is bound to it by its name, section 3.1.3.1) |
| `amq.direct` | direct | the queues bound with a binding key equal to the routing key |
| `amq.fanout` | fanout | every queue bound to it |
| `amq.topic` | topic | **the bus's topic space**: a publish is `MessageBus.Publish` on the canonical topic `AmqpTopicExchangeNames.ToCanonicalTopic` maps the routing key to, and a binding is the bus's `BindQueue` to the filter `ToCanonicalFilter` maps the binding key to (ADR-0100 decision 3.2) |
| `amq.headers`, `amq.match` | headers | BL-732 |
| declared exchanges | direct, fanout, topic; headers with BL-732 | inside the broker, onto bus queues with `MessageBus.PublishToQueue` (ADR-0100 decision 3.2's "other exchanges" row); a declared topic exchange matches keys itself, by section 3.1.3.3's rules, and is not the bus's topic space |

- **`exchange.declare`** (40.10):
  - a name that is not a non-empty run of letters, digits, `-`, `_`, `.` and `:` is a channel
    exception **`406`** (rule `declare`/`syntax`);
  - a name beginning `amq.` that does not exist, declared without `passive`, is **`403`** (rule
    `declare`/`reserved`); the default exchange cannot be declared at all (rule
    `exchange`/`default-access`), and naming `""` is **`403`**;
  - `passive` and the exchange missing: **`404`**;
  - a type other than `direct`, `fanout`, `topic` (and `headers` once BL-732 serves it): connection
    exception **`503` `COMMAND_INVALID`** (rule `declare`/`support`);
  - an existing exchange redeclared with another type: connection exception **`530`
    `NOT_ALLOWED`** (rule `declare`/`typed`); with the same type but another `durable` or other
    `arguments`: channel exception **`406`**, as `queue.declare` treats unequal flags, since nothing
    else would tell the client its declaration was not what it got;
  - `durable` is kept; until BL-732 a durable exchange lives for the server's life and is not
    persisted. `reserved-2` (auto-delete) and `reserved-3` (internal) are read and ignored, as
    0-9-1 reserves them. `arguments` mean nothing to the broker and are kept only for the equality
    check;
  - answered `declare-ok` (40.11) unless `no-wait` is set.
- **`exchange.delete`** (40.20): missing **`404`** (rule `delete`/`exists`); a pre-declared
  exchange **`403`**; `if-unused` while it has bindings **`406`** (rule `delete`/`in-use`);
  otherwise its bindings go with it and `delete-ok` (40.21) answers unless `no-wait`.
- **Queues are the bus's named queues** (ADR-0100 decision 3.3), shared with `amqp1` and `stomp`
  and with `--queue` declarations (ADR-0102 decision 3.2).
- **`queue.declare`** (50.10):
  - **an empty name** makes a server-named queue, `amq.gen-` followed by 22 base64url characters
    from `RandomNumberGenerator`, unique (the field's rule: "the server MUST create a new queue with
    a unique generated name"); `amq.` is reserved to the server, so no client can take one;
  - a name the declare would **create** must be a run of letters, digits, `-`, `_`, `.` and `:`, at
    most 255 bytes, else **`406`** (rule `declare`/`syntax`); a queue that already exists on the bus
    under another name (made over `stomp` or by `--queue`, say `orders/new`) is reachable by its name
    for every other method;
  - a name beginning `amq.` that does not exist, without `passive`: **`403`** (rule
    `declare`/`reserved`);
  - `passive` and the queue missing: **`404`** (rule `declare`/`passive`), checked without creating
    it (BL-730's queue inspection);
  - `durable`, `exclusive` and `auto-delete` are the bus's `BusQueueFlags`; an existing queue with
    other flags is **`406`** (`QueueFlagsDiffer`; the PDF's "equivalent" declaration rule), so a
    client declaring `--queue orders;durable` without `durable` is told so; an exclusive queue of
    another open connection is **`405` `RESOURCE_LOCKED`** (rule `declare`/`exclusive`);
  - `arguments` are read and ignored: 0-9-1 defines none, and RabbitMQ's `x-*` arguments are not
    served (Context);
  - an exclusive queue is deleted when its connection closes, an auto-delete queue when its last
    consumer ends, as the bus does; neither persists (ADR-0100 decision 3.7);
  - `declare-ok` (50.11) carries the name, the ready message count and the consumer count, unless
    `no-wait`;
  - the channel remembers the last queue it declared, for the methods below that name `""`.
- **`queue.bind`** (50.20) and **`queue.unbind`** (50.50): a queue name `""` means the channel's
  last declared queue, and with none it is **`404`** (rule `bind`/`queue-known`); a missing queue
  or exchange is **`404`** (rules `must-exist`, `exchange-existence`); binding to the default
  exchange is **`403`** (rule `exchange`/`default-access`); an exclusive queue of another
  connection is **`405`**. On `amq.topic` the binding is the bus's: topic access denying `sub` on
  the filter is **`403`** (ADR-0102 decision 3.4), and a binding key with no canonical form (a word
  holding `/` or `+`, or an empty word) binds nothing and still answers `bind-ok`, because the only
  routing keys it could match are unroutable themselves (ADR-0100 decision 3.2). On other exchanges
  the binding is the broker's own row. Unbinding what is not bound answers `unbind-ok`.
- **`queue.purge`** (50.30): `""` as for bind; missing **`404`**; exclusive elsewhere **`405`**;
  removes the ready messages, not the unacknowledged ones, and answers `purge-ok` (50.31) with the
  count removed (BL-730).
- **`queue.delete`** (50.40): `""` as for bind; missing **`404`**; a queue declared with
  `--queue` is **`403`** (ADR-0102 decision 3.2); `if-unused` with consumers or `if-empty` with
  messages is **`406`** (rules `delete`/`in-use`, `not-empty`; checked and deleted as one bus
  operation, BL-730); otherwise `delete-ok` (50.41) with the number of messages deleted. Each
  consumer of the deleted queue, on any connection, ends: a client that announced
  `consumer_cancel_notify` receives `basic.cancel` (60.30) with its consumer tag and `no-wait` set;
  any other client just stops receiving.
- **`no-wait`** is honoured on every method that has it: no `-ok` is sent, and a failure is still
  the exception the method would have raised.

### 6. The basic class, `mandatory`, and storage (XML class `basic`; section 4.2.6)

**Publishing.**

- **`basic.publish`** (60.40) is followed by a content header frame (class 60, weight 0, the body
  size, the property flags and the properties) and body frames until the body size is reached
  (section 4.2.6); a body size of 0 has no body frame. A method, or a content frame of another
  channel's content, arriving on this channel before its content is complete is a connection
  exception **`505` `UNEXPECTED_FRAME`**, as is a content header or body frame no publish
  announced, a header whose class is not 60, or body frames past the body size.
- **A body size above `--max-filesize`** (the bus's `MaxMessageBytes`) is a channel exception
  **`311` `CONTENT_TOO_LARGE`** as soon as the header is read; the body frames are read and
  discarded, never held (ADR-0100 decision 3.6).
- **An exchange that does not exist** is a channel exception **`404`** (rule `publish`/`must-exist`).
- **Routing** is decision 5's table. A routing key on `amq.topic` with no canonical form is
  unroutable. A publish is **routed** when at least one queue was chosen for it, whether or not that
  queue had room; on `amq.topic`, when the bus's `BusPublishResult` reports `QueuedCount +
  FullQueueCount` above 0. Live subscribers of other protocols on `amq.topic` receive it either way,
  but are not queues.
- **`mandatory`**: an unroutable publish with `mandatory` set is sent back with **`basic.return`**
  (60.50) reply code **`312` `NO_ROUTE`**, text `NO_ROUTE`, the exchange and routing key, then its
  content header and body as published; without `mandatory` it is dropped and noted in the exchange
  log. 312 is the code 0-8 defined and ADR-0100 decision 3.2 already pins; the 0-9-1 XML lists no
  code for a return, so 312 is kept.
- **`immediate`**: a routed publish with `immediate` set is put only into the chosen queues that
  have a consumer able to take it now (BL-730's publish to consumed queues); when none does, it is
  returned with **`313` `NO_CONSUMERS`** (the XML's constant). When a publish is both unroutable and
  `immediate`, 312 is sent.
- **The bus's answer to the publish:**

  | Outcome | Answer |
  | --- | --- |
  | `Succeeded` | none (publishes are not acknowledged outside confirm mode, BL-731) |
  | `AccessDenied` (topic access, or `pub` on `$queue/<q>` for a direct publish) | channel exception **`403`** (ADR-0102 decision 3.4) |
  | `MessageTooLarge` | channel exception **`311`** |
  | `StoreFull`, `TooManyTopics` | connection exception **`506` `RESOURCE_ERROR`** |
  | `QueueFull` for a chosen queue, the message transient (`delivery-mode` absent or 1) | that queue does not take it, noted; the rest of the publish stands (ADR-0100 decision 3.6; basic rule 05) |
  | `QueueFull` for a chosen queue, the message persistent (`delivery-mode` 2) | connection exception **`506`**: basic rule 02 forbids discarding a persistent message on overflow, and closing tells the publisher it was not taken |
  | `UnknownQueue` on the default exchange | unroutable, as above |

- **Properties onto the bus message** (ADR-0100 decision 3.4): `content-type` is the content
  type; `headers` entries whose value is a longstr (`S`) or shortstr (`s`) are the headers, in
  order, others dropped and noted; `expiration`, a decimal count of milliseconds from 0 to
  2147483647, is the expiry from the publish time, and any other text is a channel exception
  **`406`**; `reply-to` is the response topic when `AmqpTopicExchangeNames.ToCanonicalTopic` maps it,
  otherwise dropped; `correlation-id` is the correlation bytes (its UTF-8); the quality of service is
  1 (ADR-0100's table). `delivery-mode` decides only the `QueueFull` row above: every message in a
  durable queue persists (ADR-0100's durable row). `priority` 5 to 9 marks the message **high
  priority** and 0 to 4 or absent **normal**: basic rule 06 requires "at least 2 priority levels
  ... 0-4 and 5-9", so a queue delivers its high messages before its normal ones, each level in
  order (basic rule 08); BL-730 adds the level to the bus's queue, amending ADR-0100 decision 3.3's
  plain FIFO. A topic's fan-out and log stay in publish order.
- **The native form** (BL-706): beside those fields the broker stores, in format `amqp0`, the
  exchange name and routing key as two shortstrs followed by the content header's property flags
  and property list **byte for byte** as received. Nothing else is kept: the body is the payload.

**Delivering.**

- **Delivery tags** are per channel, from 1, one sequence for consumer deliveries and `get-ok`
  together, never 0 (the XML's `delivery-tag` rules); the broker maps each to its bus delivery.
- **`basic.consume`** (60.20): a queue `""` is the channel's last declared queue, else **`404`**; a
  missing queue **`404`**; an exclusive queue of another connection **`405`**; a consumer tag already
  used on the channel is connection exception **`530`** (rule `consume`/`01`, `not-allowed`); an
  empty tag gets `amq.ctag-` and 22 base64url characters; `exclusive` on a queue with consumers, or
  any consumer on a queue consumed exclusively, is **`403`** (rule `consume`/`01`,
  `access-refused`; BL-730's exclusive consumers); topic access denying `sub` on `$queue/<q>` is
  **`403`** (ADR-0102 decision 3.4); the bus's `TooManySubscriptions` is **`506`** (decision 7).
  `no-local` is read and ignored: the XML gives it no rule, and skipping one connection's messages
  in a shared queue would leave them at its head for every other consumer. `arguments` are
  ignored. `consume-ok` (60.21) is sent before the first delivery (the bus consumer is started
  after it).
- **`basic.deliver`** (60.60) carries the consumer tag, the delivery tag, `redelivered` (the bus's
  mark), and the exchange and routing key: from the native form when the message has one; for any
  other message, `amq.topic` and the routing key `AmqpTopicExchangeNames` maps its topic to, or, when
  the topic has no 0-9-1 form, the default exchange `""` and the queue's name. Its content header
  is the native form's properties unchanged, or for another protocol's message one built from what
  it has: `content-type`, `headers` (longstr values), `correlation-id` and `reply-to` (as the
  mapped routing key) when they fit a shortstr, `expiration` (the remaining milliseconds) and
  `timestamp` (the publish time, in seconds). The body follows in frames of at most frame-max less
  8 bytes.
- **`basic.get`** (60.70): `""`, missing and exclusive as for consume; answered `get-ok` (60.71,
  with the delivery tag, `redelivered`, exchange, routing key and the count of messages still
  ready) and the content, or `get-empty` (60.72) - one message taken from the bus at a time, held
  unacknowledged on the channel unless `no-ack` (BL-730's single get).
- **`no-ack`** consumers and gets acknowledge each delivery to the bus as it is written (basic rule
  09); otherwise the delivery waits for the client (basic rule 10).
- **`basic.ack`** (60.80): `multiple` acknowledges every outstanding tag up to the given one, and tag
  0 with `multiple` every outstanding tag; a tag not outstanding on this channel (unknown, already
  settled, or from another channel) is channel exception **`406`** (rule `ack`/`exists`).
- **`basic.reject`** (60.90): `requeue` puts the message back at its place, marked redelivered;
  otherwise it is dropped (the bus has no dead-letter queue). An unknown tag is **`406`**.
- **`basic.nack`** (60.120, RabbitMQ's extension): `reject` with `multiple`, by the same rules.
- **`basic.recover`** (60.110) and `recover-async` (60.100): with `requeue` every outstanding
  delivery of the channel goes back to its queue, marked redelivered; without it the broker sends
  the channel's outstanding deliveries again to the consumers that had them, under new tags with
  `redelivered` set, the old tags no longer outstanding (the field's text: "redelivered to the
  original recipient"). `recover` is answered `recover-ok` (60.111); `recover-async` is not.
- **`basic.cancel`** (60.30) from the client ends the consumer's deliveries and is answered
  `cancel-ok` (60.31), an unknown tag included; its outstanding deliveries stay outstanding until
  acknowledged, rejected or the channel closes (BL-730's consumer stop, since the bus's own cancel
  requeues them).
- **`basic.qos`** (60.10): `prefetch-count` bounds the channel's outstanding deliveries (all its
  consumers together), or the connection's when `global` is set; `prefetch-size` bounds their body
  bytes the same way, but never stops a first delivery while nothing is outstanding (the field's
  text); both are ignored for `no-ack` consumers. A count of 0 means the broker's own bound, 1000 a
  consumer (decision 8). Deliveries past the window wait on the channel, still unacknowledged on the
  bus, and go back to their queue when the channel closes. Answered `qos-ok` (60.11).
- **`channel.flow`** (20.20) from the client with `active` 0 stops deliveries on that channel (they
  wait as above) and `active` 1 resumes them; answered `flow-ok` with the same bit. The broker never
  sends `channel.flow` (basic rule 03 makes it optional), because its bounds refuse instead.
- **Transactions** (`tx`, 90) and **publisher confirms** (`confirm.select`, 85) are BL-731's; until
  then each is connection exception **`540` `NOT_IMPLEMENTED`**, and the capability
  `publisher_confirms` is announced only once BL-731 lands.
- **Storage across restarts is the bus's** (ADR-0100 decision 3.7): durable queues, their messages
  (transient ones too) and their `amq.topic` bindings persist under `--directory <path>` in
  `<path>/.surl/bus`, with each message's native form (BL-706); exclusive and auto-delete queues do
  not. Durable exchanges other than the pre-declared ones and their bindings persist with BL-732;
  until then they last the server's life. Without `--directory` everything is in memory.

### 7. Exceptions (XML reply-code constants)

- **A channel exception** (`soft-error`) is `channel.close` (20.40) with the code, fixed text,
  and the class and method that caused it. The broker then discards every frame on that channel
  except `channel.close-ok` and `channel.close`, and the channel number is free once `close-ok`
  arrives. The connection and its other channels go on.
- **A connection exception** (`hard-error`) is `connection.close` (10.50) with the code, fixed
  text, and the class and method. The broker then discards every frame except
  `connection.close-ok` and `connection.close`, and closes the connection when one arrives or one
  second after sending, whichever is first, so a client that never answers cannot hold it.
- `channel.close` from the client is answered `channel.close-ok` (20.41), its consumers ending and
  its outstanding deliveries going back to their queues, marked redelivered;
  `connection.close` from the client is answered `connection.close-ok` (10.51) and the connection
  ends the same way for every channel.

| Code | Kind | Sent when |
| --- | --- | --- |
| 311 `CONTENT_TOO_LARGE` | channel | body size above `--max-filesize`; the bus's `MessageTooLarge` |
| 403 `ACCESS_REFUSED` | channel; connection for a refused login | topic access (ADR-0102 decision 3.4); an `amq.` name; the default exchange; deleting a `--queue` queue; an exclusive consumer refused; a refused login (decision 2) |
| 404 `NOT_FOUND` | channel | a missing exchange or queue; `""` with no queue declared on the channel |
| 405 `RESOURCE_LOCKED` | channel | another open connection's exclusive queue |
| 406 `PRECONDITION_FAILED` | channel | bad names; unequal redeclaration; `if-unused`/`if-empty`; an unknown delivery tag; a bad `expiration` |
| 501 `FRAME_ERROR` | connection | a frame-end other than `0xCE`; a frame above frame-max; an unknown frame type; a heartbeat off channel 0 (section 4.2.3, 4.2.7) |
| 502 `SYNTAX_ERROR` | connection | a method's arguments do not decode (a short field, a bad field-table type octet, a size past the frame) |
| 503 `COMMAND_INVALID` | connection | a method only a client receives (`basic.deliver`, `connection.start`, ...) sent by the client; an unsupported exchange type |
| 504 `CHANNEL_ERROR` | connection | a method on a channel not open; `channel.open` on an open channel (rule `channel.open`/`state`); a channel number above the negotiated `channel-max` |
| 505 `UNEXPECTED_FRAME` | connection | decision 6's content framing faults |
| 506 `RESOURCE_ERROR` | connection | the bus's `StoreFull`, `TooManyTopics`, `TooManyQueues`, `TooManySubscriptions`; a persistent message a full queue refused; decision 8's broker bounds |
| 530 `NOT_ALLOWED` | connection | an unknown virtual host; a reused consumer tag; an exchange redeclared with another type |
| 540 `NOT_IMPLEMENTED` | connection | a class or method the broker does not serve: `tx` and `confirm` until BL-731, `exchange.bind`/`unbind` (40.30, 40.40) and every other extension |
| 541 `INTERNAL_ERROR` | connection | a fault inside the broker, also noted with `IExchangeLog.Note` |
| 320 `CONNECTION_FORCED` | connection | `--idle-timeout`, `--max-time`, or the server stopping, once `open-ok` was sent |

- **Before `connection.open-ok`** no exception is negotiated except decision 2's `403` and decision
  4's `530`: every other fault closes the connection with no bytes.
- **`ADR-0100` decision 3.6 is amended**: its AMQP 0-9-1 column wrote `506` as a channel close; the
  XML classes `resource-error` as a `hard-error`, so `StoreFull`, `TooManyTopics`, `TooManyQueues`
  and `TooManySubscriptions` are connection exceptions here. `311` stays a channel one.
- **Every reply text** is the code's name, ` - ` and a fixed phrase from the broker's table, never
  a queue, exchange, routing key, account, path or exception text (ADR-0006 section 3).

### 8. `amqps`, and the ADR-0006 limits

- **`amqps`** is the broker behind implicit TLS from the first byte (IANA `amqps`, port 5671),
  wrapped in `ImplicitTlsSchemeServer` by BL-507 as `mqtts` is (ADR-0010): TLS 1.2 and 1.3
  (ADR-0006 section 4), `--cert` or `--self-signed` required (`CertificateProblem` (58) without), no
  client certificate asked for (so no `EXTERNAL` mechanism), no ALPN agreed. Inside it everything
  above holds, and the policy's plain-text mechanisms are offered (decision 2).

| ADR-0006 limit or bound | How the broker applies it | Answer when hit |
| --- | --- | --- |
| `--max-message` (1 MiB) | the frame-max offered is the smaller of 131072 and it, never below 4096; a frame above the negotiated frame-max (4096 before `tune-ok`) | `501` after `open-ok`; before it, no bytes |
| `--max-filesize` (100 MiB) | one message's body size; all partly read content on one connection together | `311` (channel); `506` (connection) |
| `--head-timeout` (30 s) | from accept to `connection.open-ok`: header, start, secure, tune and open | close with no bytes |
| `--idle-timeout` (120 s) | no frame for that long; heartbeats offered at half of it (decision 3) | `320` |
| `--max-time` (3600 s), `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s | `320` once `open-ok` was sent, otherwise no bytes |
| channels a connection: 256 (`channel-max`) | decision 3 | `504` |
| outstanding deliveries a consumer: 1000 when `prefetch-count` is 0 or larger | decision 6 (the bus consumer's `maxUnacknowledged`) | none: the rest wait in the queue |
| consumers a connection: the bus's `DefaultMaxSubscriptionsPerConnection` (1000) | decision 6 | `506` |
| exchanges declared on the server: 1000; bindings on the broker's own exchanges: 100000 | decision 5 | `506` |
| the bus's bounds (`StoreFull`, `TooManyTopics`, `QueueFull`, `TooManyQueues`) | ADR-0100 decision 3.6, as amended in decision 7 | decision 6's table |

- No new option is added: ADR-0006's options and the bus's bounds cover every store a peer can
  fill; 256, 131072, 4096, 1000 and 100000 are named constants of the library.

## Alternatives considered

- **One virtual host per account, or any name accepted as `/`.** Rejected: per-account hosts must
  be fully separate (the XML's `virtual-host` text), which would split the shared bus; accepting
  any name would tell a client it reached a separate host when it did not.
- **Closing the socket on a refused login, as RabbitMQ does for clients without
  `authentication_failure_close`.** Rejected: the specification gives every client
  `connection.close`, and `403` says why; a bare close looks like a network fault.
- **Offering `PLAIN` on clear text only with `--allow-plaintext-auth`, also under
  `--allow-anonymous`.** Rejected: 0-9-1 clients log in with `PLAIN`, so an anonymous broker
  would refuse every one of them; the password is not checked there, and ADR-0032 warns at start.
- **`506` as a channel exception, as ADR-0100 first wrote.** Rejected: the XML classes it
  `hard-error`; a client that receives `channel.close` 506 is entitled to treat it as a protocol
  violation.
- **Plain FIFO queues.** Rejected: basic rule 06 is a MUST; two levels are the least it allows and
  the cheapest to add to the bus.
- **`basic.get` by a short-lived bus consumer.** Rejected: ending the consumer would requeue the
  message it just handed out, so `get` needs its own bus operation (BL-730).
- **Leaving out `immediate`, transactions, confirms or the headers exchange, as several 0-9-1
  brokers do.** Rejected by root `CLAUDE.md` ("nothing is left out because it is hard"); each is
  served, the larger ones in their own tasks (BL-731, BL-732) so each task stays one run.
- **Serving `exchange.bind` and the other RabbitMQ extensions.** Not now: no client the Context
  names needs them, they are not in the pinned specification, and `540` tells a client so.

## Consequences

- `surl amqp://127.0.0.1:0/` (with `--allow-anonymous`, or an account over TLS or with
  `--allow-plaintext-auth`) is an AMQP 0-9-1 broker over the shared bus: one virtual host `/`, the
  standard exchanges, queues shared with `amqp1` and `stomp`, and `amq.topic` shared with every
  topic protocol; `amqps://` is the same over TLS.
- `Surl.MessageBus` gains queue inspection, purge, single get, conditional delete, exclusive
  consumers, a publish to consumed queues only, a consumer stop that keeps its deliveries, and two
  priority levels (BL-730); BL-506 depends on it and on BL-706's native form.
- Transactions and publisher confirms wait for BL-731; the headers exchange and durable exchanges
  across restarts for BL-732.
- The requirement matrix [`Amqp0.md`](../../Conformance/Extended/Amqp0.md) traces each
  server-side requirement to BL-504, BL-505, BL-506, BL-730, BL-731 or BL-732; each task replaces
  its ID with the named test.
