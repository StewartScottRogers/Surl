# ADR-0093 — How the MQTT 5 broker answers `mqtt5`, `mqtt5s` and `mqtt5+ws`

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-494. BL-495 builds the packet and property codec (decisions 6 and 8), BL-496 CONNECT,
  CONNACK, logins and keep alive (decisions 1, 2 and 8), BL-497 PUBLISH, the QoS flows, retained
  messages, topic aliases and expiry (decisions 4 and 8), BL-498 SUBSCRIBE and UNSUBSCRIBE
  (decision 5), BL-499 sessions, DISCONNECT and AUTH (decisions 2, 3 and 6), BL-500 `mqtt5+ws`
  (decision 7), and BL-501 the command line and the composition (decisions 0, 7 and 9).
- **Amended:** decision 0 by [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  (2026-11-04, BL-622): the sentence "Retained messages are not shared with `mqtt`" no longer
  holds. `mqtt5`'s retained messages, live routing and shared subscriptions go through the shared
  `Surl.MessageBus`, so a message published on `mqtt://` is seen on `mqtt5://` and the other way
  round; `Mqtt5RetainedMessages` and `<path>/.surl/mqtt5/retained-messages` are not built
  (ADR-0100 decision 3.9). Sessions, the QoS flows, topic aliases and queued messages stay in the
  broker.
- **Amended:** decisions 1, 5 and 6 (2026-11-05, BL-651), to say what MQTT 5.0 says and what
  BL-495's codec already does: a property not allowed in its packet is a Malformed Packet, `0x81`
  (2.2.2.2), not a Protocol Error; a property twice that may appear once stays `0x82`; an empty
  SUBSCRIBE or UNSUBSCRIBE payload is a Protocol Error, `0x82` (`[MQTT-3.8.3-2]`,
  `[MQTT-3.10.3-2]`), not a Malformed Packet; and a Maximum QoS or Retain Handling of 3 is a
  Protocol Error, `0x82` (3.8.3.1), the reserved option bits alone staying `0x81`
  (`[MQTT-3.8.3-5]`). Decision 6's table now lists each.
- **Amends:** [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decision 4's table: Mqtt5 reaches `ISaslAuthenticationPolicy` too, for enhanced authentication
  (decision 2). [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 5's "MQTT closes
  without a reply" stays true for `mqtt` and `mqtts`; for the MQTT 5 broker a limit is answered in
  MQTT 5's own words (decision 8). [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  and [ADR-0014](ADR-0014-how-the-mqtt-server-answers.md) apply as written: `mqtt` and `mqtts`
  do not change.

## Context

`Surl.Extended.Protocol.Mqtt5` (in `Protocols/Messaging/`, ADR-0083's table) answers the `mqtt5`,
`mqtt5s` and `mqtt5+ws` schemes the Curl port adds. By ADR-0083 decision 1 it is judged by its
specification, **OASIS MQTT Version 5.0, OASIS Standard, 7 March 2019** (below "MQTT 5.0"), not by
upstream curl, whose `mqtt` is MQTT 3.1.1, and not by the Curl port. Its normative statements
`[MQTT-x.y.z-n]` are the IDs of the requirement matrix
[`Documentation/Conformance/Extended/Mqtt5.md`](../../Conformance/Extended/Mqtt5.md).

The port's `Curl.Extended.Protocol.Mqtt5.UnitLibrary` and its matrices `Mqtt5.md`,
`Mqtt5-PublishSubscribe.md` and `Mqtt5-Transports.md` were read only to learn which packets a
client sends, so that the broker accepts them: one CONNECT with Clean Start 1 and no Topic Alias
Maximum, then either one PUBLISH followed by DISCONNECT `0x00`, or one SUBSCRIBE (packet
identifier 1) after which the client reads PUBLISHes, acknowledging QoS 1 and 2, pinging at half
a Server Keep Alive, until the **server** sends DISCONNECT; `mqtt5+ws` asks for `GET /mqtt`
offering the subprotocol `mqtt`. Nothing below is taken from the port's output, and no expected
byte comes from it.

What MQTT 5.0 fixes for a server, by chapter:

| MQTT 5.0 | Fixes |
| --- | --- |
| 1.5 | The data representations: integers, the Variable Byte Integer, UTF-8 strings (`[MQTT-1.5.4-1]` to `-3`), Binary Data, String Pairs |
| 2.1, 2.2, 2.4 | The fixed header and its reserved flags, Packet Identifiers, properties (table 2-4), reason codes (table 2-6) |
| 3.1 to 3.15 | Each control packet, its properties and its reason codes |
| 4.1 | Session State |
| 4.3 to 4.6 | QoS flows, redelivery, message receipt, ordering |
| 4.7, 4.8 | Topic names and filters, wildcards, `$` topics, shared subscriptions |
| 4.9 | Flow control (Receive Maximum) |
| 4.10, 4.11 | Request/Response and server redirection (both optional for a server) |
| 4.12 | Enhanced authentication and re-authentication |
| 4.13 | Handling errors: Malformed Packet and Protocol Error |
| 5 | Security (TLS, chapter 5.4) |
| 6 | WebSocket as a transport |

## Decision

### 0. What the broker is, and how it stays apart from the MQTT 3.1.1 server

- **A broker, not a fetch.** Unlike ADR-0014's one-shot `mqtt` server, the MQTT 5 broker keeps a
  subscription until UNSUBSCRIBE, the connection's end (Clean Start 1 and Session Expiry 0), or the
  session's expiry, and delivers every later matching PUBLISH to it, because sessions, shared
  subscriptions, No Local, Receive Maximum and queued messages (the task's decisions 3 to 5)
  mean nothing without live subscribers. The one-shot read a client like curl's `mqtt` needs is
  opt-in (decision 9).
- **Isolation.** The library references `Surl.Protocol.Abstractions`, `Surl.Content` (the retained
  message file, below), `Surl.HttpMessage` and `Surl.WebSocket` (decision 7); never
  `Surl.Protocol.Mqtt` (ADR-0083, "Isolation"). `MqttPacketReader`, `MqttPacketEncoder`,
  `MqttTopicFilter` and `MqttRetainedMessages` may be **copied** and renamed `Mqtt5…`; each MQTT 5
  difference is made explicit in the copy: properties after every variable header, reason codes
  in every acknowledgement, a server DISCONNECT, a PUBLISH's RETAIN flag honoured (ADR-0014
  decision 1 kept every PUBLISH; MQTT 5.0 `[MQTT-3.3.1-5]` to `-8` do not let a broker), and
  `$share/`.
- **Retained messages are not shared with `mqtt`.** `mqtt5`'s retained messages live in their own
  `Mqtt5RetainedMessages`, not in `MqttRetainedMessages`: the isolation rule forbids the reference,
  an MQTT 5 retained message carries properties (Payload Format Indicator, Message Expiry
  Interval, Content Type, Response Topic, Correlation Data, User Properties) that the 3.1.1 store
  and file cannot hold, and the two servers honour RETAIN differently. A message published on
  `mqtt://` is therefore not seen on `mqtt5://`, and the other way round. With `--directory
  <path>` they are kept in `<path>/.surl/mqtt5/retained-messages` (ADR-0031 decision 6's rule:
  rewritten whole through a temporary name after each change, through `IContentFileSystem`), in
  memory otherwise. BL-497 fixes the file's format; a file that cannot be read stops `surl` with
  `CouldNotReadFile` (37), as `mqtt`'s does.
- **Bounds on the retained store** (ADR-0006: every store is bounded; ADR-0014 decision 7's
  numbers): 10000 topics and 104857600 payload bytes in all, constructor parameters. A retained
  PUBLISH past either is not kept and is answered `0x97` Quota exceeded (PUBACK or PUBREC for QoS
  1 and 2; a QoS 0 one is delivered to subscribers and not kept, with nothing to answer).
- **Shared state** (sessions, subscriptions, retained messages, assigned identifiers) is one
  `Mqtt5BrokerState` owned by the server instance and shared by every connection it answers, as
  `MqttRetainedMessages` is for 3.1.1; never a static. `mqtt5`, `mqtt5s` and `mqtt5+ws` listeners
  of one `surl` run share one broker (BL-501 creates it once), so a client on `mqtt5+ws` receives
  what a client on `mqtt5s` publishes.

### 1. CONNECT and the CONNACK properties (MQTT 5.0 3.1, 3.2)

**CONNECT checks** (3.1.2, 3.1.4), in order; each failure is answered as 4.13 says (decision 6):
the first packet is CONNECT (`[MQTT-3.1.0-1]`, otherwise close with no bytes, since no CONNACK may
come first); protocol name `MQTT` and level 5 (level 3 or 4, or `MQIsdp`, is CONNACK `0x84`
Unsupported Protocol Version, `[MQTT-3.1.2-2]`; any other name closes with no bytes,
`[MQTT-3.1.2-1]` leaves it to the server and an unknown protocol earns no MQTT answer); the
reserved flag 0 (`[MQTT-3.1.2-3]`, `0x81`); Will QoS and Will Retain 0 without the Will Flag, Will
QoS not 3 (`0x81`); the payload's fields present exactly as the flags say, in order
(`[MQTT-3.1.3-1]`, `[MQTT-3.1.2-16]` to `-19`, `0x81`); only the properties of table 2-4 allowed
in CONNECT (`0x81` Malformed Packet, 2.2.2.2), none twice (`0x82` Protocol Error, 2.2.2.2); Receive Maximum 0, Maximum Packet Size 0,
or Request Response/Problem Information above 1, `0x82` (3.1.2.11). A password without a user name
is legal in MQTT 5.0 (3.1.2.9) and is not refused as malformed.

**Client Identifier** (3.1.3.1): any identifier of 1 to 65535 bytes of valid UTF-8 is accepted,
which includes the 1 to 23 alphanumerics `[MQTT-3.1.3-5]` requires. A zero-length one is
accepted whatever Clean Start says (`[MQTT-3.1.3-6]`) and replaced by an **Assigned Client
Identifier** `surl-` followed by 32 lower-case hex digits of 16 bytes from
`RandomNumberGenerator`, checked unused among live sessions (`[MQTT-3.2.2-16]`) and returned in
CONNACK (`[MQTT-3.1.3-7]`). No identifier is refused, so `0x85` is never sent.

**Session takeover** (`[MQTT-3.1.4-3]`): a CONNECT for an identifier already connected sends the
older connection DISCONNECT `0x8E` Session taken over and closes it; its Will is handled as
3.1.2.5 says.

**The CONNACK of an accepted CONNECT** (3.2.2.3) carries, in this order, only these properties:

| Property | Sent | Value and why |
| --- | --- | --- |
| Session Expiry Interval (0x11) | when the broker lowers the client's | the client's value capped at **3600 s** (`Mqtt5Broker.MaxSessionExpirySeconds`): a session outlives its connection in memory, so it is bounded like any store; an hour is ADR-0006's `--max-time`. `0xFFFFFFFF` (never expires) is capped too. |
| Receive Maximum (0x21) | always | **100**: the QoS 1 and 2 PUBLISHes the broker accepts unacknowledged from one client (decision 4). The default 65535 would let one client pin 65535 messages in flight. |
| Maximum QoS (0x24) | never | absent means 2; the broker serves QoS 0, 1 and 2 (`[MQTT-3.2.2-9]` binds only a server that does not). |
| Retain Available (0x25) | never | absent means 1. |
| Maximum Packet Size (0x27) | when `--max-message` is not 0 | `ExchangeLimits.MaxMessageBytes` (default 1048576), the whole packet (decision 8). |
| Assigned Client Identifier (0x12) | when the client sent a zero-length one | as above. |
| Topic Alias Maximum (0x22) | always | **10**: aliases the client may use toward the broker (decision 4). Each holds a topic of up to 65535 bytes, so ten bound the table at 640 KiB a connection. |
| Reason String (0x1F) | never on success | |
| User Property (0x26) | never | |
| Wildcard Subscription Available (0x28) | never | absent means 1. |
| Subscription Identifiers Available (0x29) | never | absent means 1. |
| Shared Subscription Available (0x2A) | never | absent means 1. |
| Server Keep Alive (0x13) | when the client's Keep Alive is 0 or above 60 | **60** (`Mqtt5Broker.MaxKeepAliveSeconds`): half ADR-0006's 120-s idle timeout, so a client that pings when told is never idled out by `Surl.Core`; a client asking for 0 (no keep alive) would otherwise be closed by the idle timeout without a word. 60 is also the Keep Alive upstream curl's `mqtt` sends (ADR-0014). |
| Response Information (0x1A) | never | 4.10 makes Request/Response a client convention; the broker offers no response topic, which 3.1.2.11.6 allows (`[MQTT-3.1.2-28]` holds trivially). |
| Server Reference (0x1C) | never | no redirection (4.11). |
| Authentication Method (0x15), Authentication Data (0x16) | under enhanced authentication | decision 2 (`[MQTT-4.12.0-5]`, `[MQTT-4.12.0-6]`). |

Session Present (3.2.2.1.1) is 1 only when Clean Start was 0 and a session for the identifier
existed (`[MQTT-3.2.2-2]`, `[MQTT-3.2.2-3]`); every refusal has Session Present 0
(`[MQTT-3.2.2-6]`). No property the client's Maximum Packet Size cannot hold is sent: the Reason
String and User Properties are dropped first, as `[MQTT-3.2.2-19]` and `-20` say; a CONNACK that
still does not fit is sent anyway, since the broker must answer and the client asked for a size
below MQTT's minimum useful packet.

**Keep alive** (3.1.2.10, `[MQTT-3.1.2-22]`): the broker closes a connection from which no packet
has arrived for one and a half times the Keep Alive in force (the client's, or the Server Keep
Alive it sent, `[MQTT-3.2.2-22]`), sending DISCONNECT `0x8D` Keep Alive timeout first, as 4.13
allows, and publishing the Will. The clock is `ExchangeContext.TimeProvider`.

### 2. Authentication (MQTT 5.0 3.1.2.8, 3.1.2.9, 3.1.3.5, 3.1.3.6, 4.12, chapter 5)

- **Secure by default, as ADR-0032 decides for every server.** A CONNECT without an Authentication
  Method passes its user name and password, with the connection's `TlsSession`, to the
  `IAuthenticationPolicy` `Surl.Console` passes in (the 3.1.1 server's `MqttConnectJudge` does the
  same; ADR-0084 decision 4). Its verdicts map to CONNACK as the 3.1.1 server's map to return codes
  4 and 5:

  | Verdict | CONNACK |
  | --- | --- |
  | Accepted (an account matched), or accepted unchecked under `--allow-anonymous` | `0x00` Success |
  | A user name was given and refused (no such account, wrong password, no accounts at all: ADR-0032 section 5 lets none be told apart) | `0x86` Bad User Name or Password |
  | No user name; or a password over a connection with no TLS session without `--allow-plaintext-auth` (FR-028), which covers `mqtt5://` and `mqtt5+ws://` | `0x87` Not authorized |

  A refusal comes after the policy's fixed one-second delay (ADR-0032 section 8), has no Reason
  String naming the cause, and closes the connection (`[MQTT-3.2.2-7]`).
- **Enhanced authentication** (4.12): a CONNECT with an Authentication Method (0x15) is judged by
  that method's exchange alone; its user name and password fields, if any, are not checked. The
  methods offered are **`SCRAM-SHA-256` and `SCRAM-SHA-1`**, the SASL mechanism names of RFC 7677
  and RFC 5802 used as the method name (MQTT 5.0 4.12 leaves the names to agreement and names
  SCRAM as an example), run through `ISaslAuthenticationPolicy` (BL-416's SCRAM exchanges in
  `Surl.Authentication`): the broker asks `GetSaslMechanisms` for the connection and serves a
  method only if it is one of those two and the policy offers it. Any other method, `PLAIN`
  included (its password already has the CONNECT field), is CONNACK **`0x8C`** Bad authentication
  method and a close (`[MQTT-4.12.0-1]`).
  - The CONNECT's Authentication Data is SCRAM's client-first message; each server challenge goes
    in an AUTH `0x18` Continue authentication carrying the method and the data
    (`[MQTT-4.12.0-2]`, `[MQTT-4.12.0-5]`); each client AUTH must be `0x18` with the same method
    (`[MQTT-4.12.0-3]`), another reason code or method being `0x82` Protocol Error. Success is
    CONNACK `0x00` with the method and SCRAM's server-final message as Authentication Data. A
    failed exchange is CONNACK `0x87` Not authorized after the policy's delay, then close
    (`[MQTT-4.12.0-4]`); `0x87` rather than `0x86` because no user name and password were judged.
  - The whole exchange must finish within `--head-timeout`, ADR-0006's slow-sender defence for a
    first packet; past it the connection closes with no bytes. While it runs, a packet other than
    AUTH or DISCONNECT from the client (`[MQTT-3.1.2-30]`) is `0x82` and a close.
  - **Re-authentication** (4.12.1): after a CONNECT that used a method, a client AUTH `0x19`
    Re-authenticate with the same method (`[MQTT-4.12.1-1]`; another method is `0x82`) runs a new
    exchange while other packets keep flowing; success is AUTH `0x00` with the server-final data;
    failure is DISCONNECT `0x87` and a close (`[MQTT-4.12.1-2]`). A client AUTH on a connection
    that used no method is DISCONNECT `0x82` (`[MQTT-4.12.0-7]`); the broker never sends AUTH
    then (`[MQTT-4.12.0-6]`).
  - `--allow-anonymous` does not skip an exchange the client asked for: the method runs its steps
    and the policy accepts it unchecked, as ADR-0049 decides for the mail servers' SASL.
- **TLS** (chapter 5.4): `mqtt5s` is the broker behind implicit TLS, ADR-0010's handshake by the
  engine (`ImplicitTlsSchemeServer`), TLS 1.2 and 1.3 (ADR-0006 section 4). No client certificate
  is asked for; chapter 5.4.4's identity from a certificate is not used.
- **Authorization** (5.4.2): every authenticated client may publish and subscribe to every topic;
  `0x87` Not authorized and `0x8A` are therefore never sent for a topic. Per-topic access is not
  part of the accounts file (ADR-0032), so there is nothing to check it against.

### 3. Session state (MQTT 5.0 3.1.2.4, 3.1.2.5, 3.1.2.11.2, 4.1)

- **What a session holds** (4.1): its subscriptions with their options and identifiers; QoS 1 and 2
  messages sent and not yet acknowledged; QoS 2 messages received whose PUBREL has not come;
  messages queued for it while it is disconnected; and its Will with its delay. Sessions are held
  in memory only and are lost when `surl` stops (4.1 has no requirement that sessions survive a
  server restart, and keeping them on disk would need a format for in-flight packets that no
  client can observe the difference of within one run).
- **Clean Start** (`[MQTT-3.1.2-4]` to `-6`): 1 discards any session for the identifier and starts
  a new one; 0 resumes the session if there is one (Session Present 1), redelivering its
  unacknowledged PUBLISHes with DUP 1 and its PUBRELs first, in their original order
  (`[MQTT-4.4.0-1]`, 4.6), then its queued messages, all within Receive Maximum.
- **Session Expiry Interval** (3.1.2.11.2): absent or 0 ends the session with its connection;
  otherwise it is kept for the interval (capped, decision 1) after the connection closes
  (`[MQTT-3.1.2-23]`) and discarded once it passes (`[MQTT-4.1.0-2]`), with its Will published
  then if it had not been. A DISCONNECT may change the interval, except from 0 to non-zero, which
  is `0x82` Protocol Error (3.14.2.2.2); the broker never sends the property on its own DISCONNECT
  (`[MQTT-3.14.2-2]`).
- **Queued messages for a disconnected session**: QoS 1 and 2 messages only (QoS 0 is not queued,
  which 4.1 allows), at most **1000 messages and 16777216 payload bytes (16 MiB) a session**, and
  at most **1000 sessions** kept without a connection, all constructor parameters. A message past
  a session's queue bound is dropped for that session (the oldest kept, as 4.1's non-normative
  text allows a server to drop under an administrative limit). A new session past the session
  bound is refused CONNACK `0x97` Quota exceeded. The bounds are ADR-0006's rule that every store
  a peer can fill is bounded.
- **The Will** (3.1.2.5, 3.1.3.2): kept with the session, published after its connection closes
  without DISCONNECT `0x00`, or with DISCONNECT `0x04`, once its Will Delay Interval passes or the
  session ends, whichever is first (`[MQTT-3.1.2-8]`), unless a new connection resumes the session
  first (`[MQTT-3.1.3-9]`); retained as Will Retain says (`[MQTT-3.1.2-14]`, `-15`); with its
  properties and User Properties in order (`[MQTT-3.1.3-10]`); removed once published or on
  DISCONNECT `0x00` (`[MQTT-3.1.2-10]`, `[MQTT-3.14.4-3]`). A Will is published as if its client
  sent it, so a No Local subscription of the same client identifier does not receive it.

### 4. PUBLISH: QoS, flow control, topic aliases, expiry, payload format, properties (3.3 to 3.7, 4.3 to 4.6, 4.9)

- **QoS flows** (4.3): QoS 0 is delivered; QoS 1 is answered PUBACK after the message is taken
  (`[MQTT-4.3.2-4]`); QoS 2 is answered PUBREC and taken on receipt, a repeat of the same packet
  identifier before PUBREL not delivered again (method B of 4.3.3: deliver on PUBLISH, remember
  the identifier until PUBREL, `[MQTT-4.3.3-10]`), then PUBREL is answered PUBCOMP
  (`[MQTT-4.3.3-11]`); a PUBREL for an unknown identifier is PUBCOMP `0x92` Packet Identifier not
  found. Outbound the broker is the sender of 4.3.2 and 4.3.3, with Packet Identifiers unique
  among its in-flight PUBLISHes to that session (`[MQTT-2.2.1-4]`); a client PUBACK or PUBREC of
  `0x80` or above ends the message (`[MQTT-4.4.0-2]`). A message is delivered at the lower of
  its QoS and the subscription's granted QoS (`[MQTT-3.8.4-8]`).
- **Acknowledgement reason codes the broker sends**: `0x00` Success; `0x10` No matching
  subscribers when no subscription matched (3.4.2.1 lets the server say so; the message is still
  retained if RETAIN was set); `0x97` Quota exceeded (decision 0's retained bound); `0x99` Payload
  format invalid (below); `0x91` Packet Identifier in use for a QoS 2 PUBLISH reusing the identifier
  of a QoS 1 message still in flight. Never `0x87` (decision 2, "Authorization") and never `0x90`
  Topic Name invalid: a topic name that breaks 4.7 is a Malformed Packet (decision 6). The short form (no reason code, no
  properties, 3.4.2.1) is used for `0x00` with no properties.
- **Receive Maximum both ways** (4.9): inbound, a QoS 1 or 2 PUBLISH arriving while 100 (decision
  1) are unacknowledged is DISCONNECT **`0x93`** Receive Maximum exceeded. Outbound, the broker sends
  no more unacknowledged QoS 1 and 2 PUBLISHes than the client's Receive Maximum (default 65535)
  (`[MQTT-3.3.4-9]`, `[MQTT-4.9.0-1]`, `-2`), holding the rest in the session in order, and keeps
  answering every other packet meanwhile (`[MQTT-3.3.4-10]`, `[MQTT-4.9.0-3]`).
- **Topic aliases both ways** (3.3.2.3.4): inbound, an alias of 1 to 10 with a topic sets it, an
  alias with an empty topic uses it; alias 0 or above 10 is DISCONNECT **`0x94`** Topic Alias
  invalid; an empty topic with no alias, or with an alias never set, is `0x82`. The table belongs
  to the connection, never the session (`[MQTT-3.3.2-7]`). Outbound, the broker uses aliases only
  when the client's CONNECT sent a Topic Alias Maximum above 0 (`[MQTT-3.1.2-27]`): the first
  PUBLISH of a topic on the connection gives it the next free alias with the full topic, later ones
  send the alias and an empty topic; once the client's maximum is used up the oldest alias is
  reassigned (`[MQTT-3.3.2-11]`, `[MQTT-3.1.2-26]`).
- **Message Expiry Interval** (3.3.2.3.3): a message whose interval passes before delivery to a
  subscriber has started is dropped for that subscriber (`[MQTT-3.3.2-5]`); a delivered message
  carries the interval less the time it waited (`[MQTT-3.3.2-6]`); a retained message expires and
  is removed likewise. Once a QoS 2 PUBLISH has been sent, expiry no longer applies
  (`[MQTT-4.3.3-7]`).
- **Payload Format Indicator** (3.3.2.3.2): with value 1, the payload must be valid UTF-8 (1.5.4's
  rules); otherwise the PUBLISH is answered PUBACK or PUBREC **`0x99`** Payload format invalid and
  not delivered, or for QoS 0 DISCONNECT `0x99`, which 3.3.2.3.2 permits ("MAY send ... with
  Reason Code 0x99"): the broker checks because it can, and a subscriber is then never handed a
  payload its indicator mislabels. A value above 1 is `0x82`.
- **Forwarded unaltered** (3.3.2.3): Payload Format Indicator, Response Topic, Correlation Data,
  every User Property in its order, and Content Type (`[MQTT-3.3.2-4]`, `-15` to `-18`, `-20`). The
  Topic Alias and Subscription Identifiers are per delivery; a Subscription Identifier from a
  client PUBLISH is `0x82` (`[MQTT-3.3.4-6]`).
- **RETAIN** (3.3.1.3): honoured as `[MQTT-3.3.1-5]` to `-8` say: RETAIN 1 replaces the topic's
  retained message, RETAIN 1 with an empty payload removes it and is not stored, RETAIN 0 leaves
  the store alone.
- **Ordering** (4.6): every topic is an Ordered Topic (`[MQTT-4.6.0-6]`); each connection's
  PUBLISHes are delivered to each non-shared subscriber in the order received
  (`[MQTT-4.6.0-5]`), and the broker's PUBACKs and PUBRECs go out in arrival order.

### 5. SUBSCRIBE and UNSUBSCRIBE (3.8 to 3.11, 4.7, 4.8)

- **Filters** (4.7): `#` only as the last level and alone in it, `+` alone in its level
  (`[MQTT-4.7.1-1]`, `-2`), at least one character (`[MQTT-4.7.3-1]`), no U+0000
  (`[MQTT-4.7.3-2]`); a filter breaking these gets SUBACK **`0x8F`** Topic Filter invalid for that
  filter only, the others granted. Matching is ordinal, with no normalisation
  (`[MQTT-4.7.3-4]`), and a filter starting with `#` or `+` does not match a topic starting with `$`
  (`[MQTT-4.7.2-1]`). A client may publish to a `$` topic; the broker publishes none of its own
  (no `$SYS`).
- **Granted QoS**: the requested QoS is granted as asked (SUBACK `0x00`, `0x01` or `0x02`). Reserved
  option bits set are malformed: DISCONNECT `0x81` (`[MQTT-3.8.3-5]`). A requested Maximum QoS of
  3 or a Retain Handling of 3 is a Protocol Error: DISCONNECT `0x82` (3.8.3.1). A SUBSCRIBE or
  UNSUBSCRIBE with no payload is `0x82` (`[MQTT-3.8.3-2]`, `[MQTT-3.10.3-2]`).
- **Options** (3.8.3.1): **No Local** 1 never forwards a message to the connection whose client
  identifier published it (`[MQTT-3.8.3-3]`), and on a shared subscription is `0x82`
  (`[MQTT-3.8.3-4]`); **Retain As Published** keeps or clears RETAIN on forwarding
  (`[MQTT-3.3.1-12]`, `-13`); **Retain Handling** 0 sends matching retained messages at every
  subscribe, 1 only when the subscription is new, 2 never (`[MQTT-3.3.1-9]` to `-11`). Retained
  messages are sent with RETAIN 1 whatever Retain As Published says (3.3.1.3), after the SUBACK.
- **Subscription Identifiers** (3.8.2.1.2): supported. A SUBSCRIBE's identifier (1 to 268435455;
  0 is `0x82`) is stored with each of its subscriptions and sent on every delivery they cause
  (`[MQTT-3.3.4-3]`). A message matching several of a client's non-shared subscriptions is sent
  **once**, at the highest granted QoS among them (`[MQTT-3.3.4-2]`), carrying every identifier
  of the matching subscriptions (`[MQTT-3.3.4-4]`).
- **Replacing** (`[MQTT-3.8.4-3]`, `-4`): an identical filter replaces the session's subscription,
  keeping no duplicate; Retain Handling then applies as for a new one (0 re-sends, 1 does not).
- **Shared subscriptions** (4.8.2): supported. `$share/<name>/<filter>`, the name at least one
  character and without `/`, `+` or `#` (`[MQTT-4.8.2-1]`, `-2`, otherwise `0x8F`). Each message
  matching a group goes to **one** member session, chosen round-robin among members with a
  connection and room in their Receive Maximum, else round-robin among all members (queued in that
  session); a member's granted QoS is respected (`[MQTT-4.8.2-3]`). A QoS 2 delivery interrupted
  by a lost connection is finished with that member when it reconnects (`[MQTT-4.8.2-4]`), dropped
  if its session ends (`[MQTT-4.8.2-5]`); a member's `0x80`-or-above acknowledgement discards the
  message (`[MQTT-4.8.2-6]`). Retained messages are never sent for a shared subscription
  (4.8.2, "retained messages are not sent ... when it is a Shared Subscription").
- **SUBACK** has one reason code a filter, in order (`[MQTT-3.8.4-6]`, `[MQTT-3.9.3-1]`). A
  SUBSCRIBE reusing a Packet Identifier the session has in flight from the client is answered
  `0x91` for every filter. `0x9E`, `0xA1` and `0xA2` (features not supported) are never sent,
  since all three features are supported.
- **UNSUBSCRIBE** (3.10, 3.11): each filter matching a subscription exactly deletes it, `0x00`;
  one that does not, **`0x11`** No subscription existed (`[MQTT-3.10.4-5]`); an invalid filter
  `0x8F`. Deliveries in flight complete (`[MQTT-3.10.4-3]`).
- **Bounds**: at most **1000 subscriptions a session** (constructor parameter, ADR-0006's rule); a
  filter past it is SUBACK `0x97` Quota exceeded.

### 6. Protocol errors and the broker's DISCONNECT (3.14, 4.13)

- **Before CONNACK** a failure in a readable CONNECT is answered with a CONNACK reason code and a
  close: the codes of decisions 1 and 2, `0x81` Malformed Packet, `0x82` Protocol Error and `0x95`
  Packet too large (4.13.1). A first packet that is not
  CONNECT, or bytes that are not a readable fixed header, close with no bytes (no CONNACK may answer
  a non-CONNECT, `[MQTT-3.2.0-1]`).
- **After CONNACK `0x00`** the broker answers every Malformed Packet and Protocol Error with a
  DISCONNECT carrying the reason code, then closes (`[MQTT-4.13.1-1]`, `[MQTT-4.13.2-1]`,
  `[MQTT-3.14.4-1]`, `-2`); it never sends DISCONNECT before such a CONNACK (`[MQTT-3.14.0-1]`):

  | Cause | Reason code |
  | --- | --- |
  | A malformed fixed header (reserved flags, `[MQTT-2.1.3-1]`; `[MQTT-3.6.1-1]`, `[MQTT-3.8.1-1]`, `[MQTT-3.10.1-1]`, `[MQTT-3.14.1-1]`, `[MQTT-3.15.1-1]`), a Variable Byte Integer over four bytes or not minimal (`[MQTT-1.5.5-1]`), a length past the packet, ill-formed UTF-8, U+0000, a surrogate (`[MQTT-1.5.4-1]`, `-2`), both QoS bits set (`[MQTT-3.3.1-4]`), a property of the wrong type, a property not allowed in its packet (2.2.2.2), a topic name with a wildcard (`[MQTT-3.3.2-2]`), reserved SUBSCRIBE option bits set (`[MQTT-3.8.3-5]`) | `0x81` Malformed Packet |
  | A second CONNECT (`[MQTT-3.1.0-2]`), a property twice that may appear once (2.2.2.2), an empty SUBSCRIBE or UNSUBSCRIBE payload (`[MQTT-3.8.3-2]`, `[MQTT-3.10.3-2]`), a Maximum QoS or Retain Handling of 3 (3.8.3.1), a packet only a server sends, a Packet Identifier 0, DUP on QoS 0, the AUTH and Subscription Identifier cases of decisions 2, 4 and 5, a Payload Format Indicator above 1, Session Expiry raised from 0 on DISCONNECT | `0x82` Protocol Error |
  | Receive Maximum exceeded (decision 4) | `0x93` |
  | A topic alias out of range (decision 4) | `0x94` |
  | A packet larger than the Maximum Packet Size (decision 8) | `0x95` Packet too large |
  | Keep Alive missed (decision 1) | `0x8D` |
  | The session taken over by another connection | `0x8E` |
  | A QoS 0 PUBLISH whose payload contradicts its Payload Format Indicator (decision 4) | `0x99` |
  | `surl` stopping | `0x8B` Server shutting down, written within ADR-0006 section 5's one-second write deadline |
  | Re-authentication failed (decision 2) | `0x87` |

- The broker sends no Reason String with a DISCONNECT unless the client's Request Problem
  Information allows it (`[MQTT-3.1.2-29]` permits it on DISCONNECT either way); its text is fixed,
  from the server's own table (ADR-0006 section 3), and never names a path, an account or an
  exception. Before closing for a limit or an error, the broker publishes the Will as 3.1.2.5 says
  (a closing DISCONNECT from the server is not a client's `0x00`).
- **A client DISCONNECT** (3.14.4): `0x00` closes and discards the Will (`[MQTT-3.14.4-3]`);
  `0x04` Disconnect with Will Message closes and publishes it after its delay; every other reason
  code closes as a network loss would. A Session Expiry Interval on it changes the session's
  (decision 3).

### 7. `mqtt5+ws` and `mqtt5s` (chapter 6, chapter 5.4)

- **`mqtt5+ws`** is the broker over WebSocket: the listener reads an HTTP/1.1 upgrade head with
  `Surl.HttpMessage` and checks it with `Surl.WebSocket`'s `WebSocketUpgradeChecks` (RFC 6455
  section 4.2.1), then presents the binary frames as the byte stream the broker's packet reader
  reads, so the broker's packet handling is reused unchanged.
  - **The path is `/mqtt`.** MQTT 5.0 fixes no path; `/mqtt` is the path the brokers' documentation
    uses and the path a client derives from an `mqtt5+ws://host/` URL without being told. Any other
    path is `404 Not Found`, `Connection: close`, as the WebSocket server answers a missing
    resource (ADR-0071). The listen URL names no path (its topic part is ignored, as `mqtt5://`'s).
  - **The subprotocol is `mqtt`** (`[MQTT-6.0.0-4]`): `WebSocketUpgradeAcceptance` with `["mqtt"]`
    answers `101 Switching Protocols` with `Sec-WebSocket-Protocol: mqtt`. An upgrade that does not
    offer `mqtt` (`[MQTT-6.0.0-3]` binds the client) is refused **`400 Bad Request`**,
    `Connection: close`, through a `WebSocketUpgradeRefusal`: accepting it with no subprotocol
    would leave the client unsure the endpoint speaks MQTT, and chapter 6 makes `mqtt` the only
    subprotocol. No extension is negotiated.
  - **Frames** (`[MQTT-6.0.0-1]`, `-2`): MQTT packets travel in binary frames; a packet may span
    frames and a frame may hold several packets, so the reader keeps one byte stream across them.
    A text frame closes the connection with WebSocket close code **1003** (unsupported data, RFC
    6455 section 7.4.1) and no MQTT packet. Ping is answered with pong; a client close is answered
    with close and ends the connection as a network loss (the Will is published unless DISCONNECT
    `0x00` came first). The broker writes each packet in one binary frame, unmasked, with no
    fragmentation.
  - **Logins** are MQTT's own (decision 2); no HTTP authentication is asked for on the upgrade.
    `mqtt5+ws` has no TLS, so a CONNECT password needs `--allow-plaintext-auth`. A TLS variant of
    WebSocket is not a scheme the port adds, so none is served.
  - The upgrade head is held to `--max-request-head` and `--head-timeout` (ADR-0006), and each
    frame to `--max-message`.
- **`mqtt5s`** is the broker behind implicit TLS from the first byte (chapter 5.4, IANA
  `secure-mqtt`, port 8883 by ADR-0083 decision 7), wrapped in `ImplicitTlsSchemeServer` by
  BL-501 exactly as `mqtts` is (ADR-0010); it needs `--cert` or `--self-signed`, refused as an
  implicit-TLS listen URL is (`CertificateProblem` (58)). No ALPN is required: MQTT 5.0 registers
  none, and a client offering `mqtt` is agreed.

### 8. Limits (ADR-0006) and the Maximum Packet Size

| ADR-0006 limit | How the broker applies it | Answer when hit |
| --- | --- | --- |
| `--max-message` (1 MiB) | the whole MQTT packet, fixed header included, and sent as CONNACK's Maximum Packet Size (decision 1); a WebSocket frame on `mqtt5+ws` too | a fixed header announcing more: before CONNACK, CONNACK `0x95` Packet too large and close; after it, DISCONNECT `0x95` and close (4.13: the receiver of a packet past its maximum uses `0x95`). The body is never read. |
| `--max-filesize` (100 MiB) | a PUBLISH payload | it cannot be reached past `--max-message`; when `--max-message` is 0 (no limit) a larger payload is DISCONNECT `0x95`. |
| `--head-timeout` (30 s) | from accept to CONNACK `0x00`, the TLS handshake, the WebSocket upgrade and any enhanced authentication included | close with no bytes (no CONNACK can be owed to a CONNECT that never arrived; an exchange stalled mid-way is closed, as 4.13 allows a close without a reason code). |
| `--idle-timeout` (120 s) | `Surl.Core`'s; the Keep Alive of decision 1 bites first | close with no bytes. |
| `--max-time` (3600 s) | `Surl.Core`'s, a connection's whole life | close with no bytes; the Will is published. |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s | close with no bytes (ADR-0006 section 5's row for framed protocols; there is no CONNECT to answer yet). |

- **Outbound size** (`[MQTT-3.1.2-24]`, `-25`): a PUBLISH larger than the client's Maximum Packet
  Size is not sent and is treated as delivered for that client; any acknowledgement or SUBACK
  whose Reason String or User Properties would pass it is sent without them.
- **The broker's own stores** - retained messages (decision 0), sessions, queued messages and
  subscriptions (decisions 3 and 5), topic aliases (decision 1) - are bounded by constructor
  parameters with the defaults above. **No new option** is added for any of them: ADR-0006's
  options cover the per-connection limits, and the store bounds follow ADR-0014 decision 7 and
  ADR-0092 decision 1 in being constants until someone needs to change one.
- The 3.1.1 server's limits and its bare close (ADR-0006 section 5, "MQTT closes without a reply")
  do not change.

### 9. The one-shot read: `--mqtt5-disconnect-after-retained`

A client that reads a topic as curl's `mqtt` does - SUBSCRIBE, print each PUBLISH, stop when the
server sends DISCONNECT - would wait forever on a broker (decision 0). One flag, **off by
default**, gives it ADR-0014's fetch:

- **`--mqtt5-disconnect-after-retained`** (a negatable flag, category `mqtt5`; BL-501 adds it with
  its `--aihelp` facts): after the broker answers a SUBSCRIBE with SUBACK and sends the retained
  messages its Retain Handling allows, it sends DISCONNECT **`0x00`** Normal disconnection and
  closes, discarding the client's Will as a normal end. A server may disconnect a client with
  `0x00` (3.14.2.1), so the broker stays within MQTT 5.0. Publishing is unaffected.
- **Why opt-in.** The flag makes the broker useless as a broker for every other client on that
  listener, so it is chosen, not assumed; the Curl port's integration test (BL-502) passes it,
  exactly as an operator who wants `curl`-style reads would.

## Alternatives considered

- **Share `MqttRetainedMessages` with `mqtt`.** Rejected (decision 0): the isolation rule forbids
  the reference, the 3.1.1 store holds no properties, and the 3.1.1 server keeps every PUBLISH as
  retained, which MQTT 5.0 forbids a broker.
- **One-shot by default, as ADR-0014.** Rejected: half of this ADR's decisions (sessions, shared
  subscriptions, flow control) would describe behaviour no client could reach, and MQTT 5 clients
  other than the port's expect a broker. The flag of decision 9 keeps the one-shot read.
- **Offer `PLAIN` as an enhanced-authentication method.** Rejected: MQTT 5 already carries a user
  name and password in CONNECT; a second way to send the same secret adds a path to test and no
  capability.
- **Accept an upgrade without the `mqtt` subprotocol.** Rejected (decision 7): chapter 6 makes it
  the subprotocol, and a client that did not ask for MQTT should not be talked to in MQTT.
- **Persist sessions under `--directory`.** Rejected (decision 3): no MQTT 5.0 requirement asks for
  it and in-flight packet state would need a file format of its own.

## Consequences

- `surl mqtt5://127.0.0.1:0/` is a small but real MQTT 5 broker: sessions, QoS 0 to 2, retained
  messages, aliases, shared subscriptions and SCRAM enhanced authentication, all bounded.
- `mqtt://` and `mqtt5://` are two separate worlds in one `surl`: what is published on one is not
  seen on the other.
- The requirement matrix [`Mqtt5.md`](../../Conformance/Extended/Mqtt5.md) traces every server
  statement of MQTT 5.0 to BL-495 to BL-500; each task replaces its ID with the named test.
- BL-501 adds `--mqtt5-disconnect-after-retained` and the `mqtt5` help category and `--aihelp`
  topic; the `mqtt` topic gains a cross-reference saying `mqtt5` is the MQTT 5 broker.
