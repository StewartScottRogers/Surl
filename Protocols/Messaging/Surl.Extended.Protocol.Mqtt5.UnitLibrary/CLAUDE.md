# Surl.Extended.Protocol.Mqtt5.UnitLibrary

An extended server (ADR-0083): it answers the `mqtt5`, `mqtt5s` and `mqtt5+ws` schemes the Curl
port adds, which upstream curl does not have. ADR-0093 decides how. It is judged by its
specification, OASIS MQTT Version 5.0 (7 March 2019), never by the Curl port's output, and its
unit tests replay bytes written by hand from that specification.

What is here so far is the packet and property codec (BL-495, ADR-0093 decisions 6 and 8):

- `Mqtt5PacketEncoder.Encode` writes any of the fifteen control packets - fixed header, then the
  body the packet's `WriteBody` writes through `Mqtt5BodyWriter`. A Reason Code and Property
  Length that may be left off (Success with no properties) are left off.
- `Mqtt5PacketDecoder.Decode` reads one from its first byte and body, or reports the
  `Mqtt5PacketFault` - reason code and the normative statement broken - that section 4.13 gives
  it: `0x81` for a malformation (reserved fixed-header flags, a Variable Byte Integer over four
  bytes or not minimal, a length past the packet, ill-formed UTF-8, U+0000 or a surrogate, a
  property table 2-4 does not define or does not allow in the packet (section 2.2.2.2), reserved
  Connect or Subscription Options bits, both QoS bits, a wildcard in a Topic Name, trailing
  bytes); `0x82` for a Protocol Error (a property twice that may appear once, Packet Identifier
  0, DUP at QoS 0, an empty SUBSCRIBE or UNSUBSCRIBE, a Maximum QoS or Retain Handling of 3);
  `0x84` for a CONNECT that is not `MQTT` level 5 (`Mqtt5PacketFault.UnsupportedProtocolLevel`
  and `UnknownProtocolName`, which ADR-0093 decision 1 answers differently).
- `Mqtt5PacketReader` reads packets from an `IConnection` however the bytes are split, refusing
  one over the packet limit `0x95` from its fixed header and a `PUBLISH` payload over the payload
  limit `0x95` from its prefix. It is copied from the 3.1.1 server's `MqttPacketReader`, not
  referenced.
- Table 2-4 is `Mqtt5PropertyDefinition`; `Mqtt5PropertyContext` bit `n` is packet type `n`, and
  bit 0 the Will Properties.

The decoder is structural. Which packets a client may send, and each property value's meaning
(Receive Maximum 0, a Payload Format Indicator above 1, a Subscription Identifier of 0, Topic
Filter syntax), are the broker's to judge.

The broker so far answers CONNECT and what follows it (BL-496, ADR-0093 decisions 1, 2, 6 and 8):

- `Mqtt5Broker` is the `IConnectionProtocolServer` (`mqtt5`, `mqtt5s`); its doc comment lists every
  answer. `Mqtt5BrokerConnection` answers one connection: the first packet under the head timeout,
  `Mqtt5ConnectJudge` for property values and Authentication Data without a method, the login through
  `IAuthenticationPolicy`, CONNACK with `Mqtt5ConnectAcknowledgementProperties`, then PINGREQ,
  DISCONNECT and the Keep Alive (1.5 times the one in force, then DISCONNECT `0x8D`).
- `Mqtt5BrokerState` is shared by every connection of one broker: connected client identifiers,
  assigned `surl-<32 hex>` identifiers, Wills waiting out their delay on the context's
  `TimeProvider`, and the one `Surl.MessageBus` of the run (ADR-0100 decision 3.9), on which
  every Will is published for the client's account. A client PUBLISH goes through the client's own
  `BusConnection.Publish`, so its No Local subscriptions miss it.
- Session takeover (BL-679, `[MQTT-3.1.4-3]`): `RegisterClient` for a connected identifier
  cancels the older connection's `Mqtt5ClientRegistration.TakenOverToken`, which its packet read
  is linked to, so it sends `DISCONNECT` `0x8E` and closes, settling its session and Will as for
  any other close; its registration ends last. The newer connection waits for that
  (`PreviousEnded`, within its head timeout, past it closed with no reply) before it opens the
  session, so Clean Start 0 resumes what the older one kept and Clean Start 1 discards it.

PUBLISH and the QoS flows (BL-497, ADR-0093 decision 4 as ADR-0100 decision 3.9 amends it):

- `Mqtt5InboundPublishes` is one connection's inbound state - the client's Topic Aliases and (the session's) the
  QoS 2 Packet Identifiers awaiting PUBREL (method B) - and judges each PUBLISH:
  Subscription Identifier or Payload Format Indicator above 1 `0x82`, alias 0 or above 10 `0x94`,
  empty topic with no alias set `0x82`, unmappable topic `0x81`, a QoS 2 repeat answered PUBREC
  without delivering, the 101st QoS 2 awaiting PUBREL `0x93`, mislabelled UTF-8 `0x99`.
- The message goes to the bus through `Mqtt5BusMessages` (Payload Format Indicator as
  `Utf8Payload`, Message Expiry Interval as an absolute `ExpiresAt`, Content Type, Response Topic,
  Correlation Data and User Properties in order). Its outcome is the PUBACK or PUBREC code:
  `0x10` when no subscription or queue took it, `0x87` topic access, `0x97` a bus bound,
  `MessageTooLarge` DISCONNECT `0x95`; a refused QoS 0 PUBLISH is dropped and noted. After a
  publish the connection calls `SaveChangesAsync` and notes a `StorageFailed`.
- `Mqtt5OutboundDeliveries` sends what a client's subscriptions receive, from the session's bounded queue of
  1000 (a message past it is dropped for that client), beside the reading of packets and through
  the connection's one-at-a-time write: at the lower QoS, within the client's Receive Maximum,
  Packet Identifiers unique among those in flight, `Mqtt5OutboundTopicAliases` when the client
  allows aliases (the oldest reassigned), the Message Expiry Interval left rounded up to whole
  seconds, and nothing that expired while it waited, that names a topic the client cannot name,
  or that is larger than the client's Maximum Packet Size.

SUBSCRIBE and UNSUBSCRIBE (BL-498, ADR-0093 decision 5):

- `Mqtt5ClientSubscriptions` is one session's subscriptions, keyed by the Topic Filter as
  sent. Each filter is a bus subscription (`$share/<g>/` a fan-out group, round robin) granted
  its Maximum QoS; an identical filter replaces it. SUBACK `0x8F` unmappable, `0x87` topic
  access, `0x97` past the bus's subscriptions a connection, every filter `0x91` when the
  Packet Identifier awaits PUBREL. Subscription Identifier 0, or No Local on a `$share/` filter,
  is DISCONNECT `0x82`.
- No Local is the bus's `noLocal` subscribe option. Retain As Published keeps a live message's
  RETAIN. Retain Handling 0 offers retained messages at every subscribe, 1 only for a new
  subscription, 2 never, and a shared subscription never; they follow the SUBACK with RETAIN 1.
- Overlap (`[MQTT-3.3.4-2]`): non-shared subscriptions gather what the bus hands them, and
  `BusConnection.AfterMessageDelivered` sends the message once, at the highest QoS, RETAIN when
  any keeps it, with every Subscription Identifier in subscription order. A shared
  subscription's delivery goes on its own, with its own identifier.
- UNSUBSCRIBE: UNSUBACK `0x00` deleted, `0x11` none existed, `0x8F` invalid, `0x91` as above.

Sessions, DISCONNECT and AUTH (BL-499, ADR-0093 decisions 2, 3 and 6):

- `Mqtt5Session` is one client's session: its `Mqtt5ClientSubscriptions` on its own
  `BusConnection`, the waiting messages (1000 and 16 MiB of payload, connected or not; QoS 0 is
  not queued while no connection holds it), the `PUBLISH`es in flight (`Mqtt5InFlightPublish`,
  with their sending order) and the QoS 2 identifiers awaiting PUBREL. `Mqtt5BrokerState` keeps
  it after its connection for its Session Expiry Interval on the context's `TimeProvider`, at most
  1000 with no connection (a new one past it is CONNACK `0x97`), and `OpenSession` resumes it for
  Clean Start 0 and the same account (Session Present 1) or ends it. A session another
  connection holds is never resumed; a takeover first waits for that connection to end.
- `Mqtt5OutboundDeliveries` is per connection, over the session: it resends a resumed session's
  in-flight PUBLISHes with DUP 1 and its PUBRELs in sending order first, and a resumed session
  holding more in flight than the new Receive Maximum owes those slots back.
- A client DISCONNECT `0x00` discards the Will, any other code keeps it; its Session Expiry
  Interval replaces the session's (capped), raising it from 0 is DISCONNECT `0x82`.
- Enhanced authentication: `Mqtt5EnhancedAuthentication` runs `SCRAM-SHA-256` and `SCRAM-SHA-1`
  through `ISaslAuthenticationPolicy` (the broker's optional third constructor argument; without
  it every method is `0x8C`). Absent Authentication Data is an empty client-first, which SCRAM
  refuses. SCRAM's server-final, which the policy issues as a second challenge, is answered with
  the empty response and carried in the CONNACK or AUTH `0x00`, as Kafka's `KafkaSaslLogin` does.
  The whole exchange runs within the head timeout. Re-authentication (AUTH `0x19`) must
  authenticate the CONNECT's account, or it is DISCONNECT `0x87`.

`mqtt5+ws` (BL-500, ADR-0093 decision 7, MQTT 5.0 chapter 6):

- `Mqtt5WebSocketServer` is the `IConnectionProtocolServer` for `mqtt5+ws`, in front of a
  `Mqtt5Broker`. It reads the upgrade with `Surl.HttpMessage`'s `HttpConnectionReader`, refuses
  one without exactly one `Host` `400`, one `WebSocketUpgradeChecks` refuses as it says, a path
  other than `/mqtt` (query ignored) `404`, and one not offering subprotocol `mqtt` `400`, each
  with `Connection: close` and a lingering close; otherwise `101` with
  `Sec-WebSocket-Protocol: mqtt`. The broker then gets what the upgrade left of the head
  timeout, so accept to CONNACK is one head timeout.
- `Mqtt5WebSocketConnection` is the `IConnection` the broker serves: binary and continuation
  payloads read as one byte stream (packets span frames, frames hold several packets), each
  write one unmasked binary frame. Text closes `1003`; unmasked, `RSV`, misplaced continuation
  or binary, and any frame RFC 6455 makes invalid `1002`; a frame over `--max-message` `1009`;
  a client close is echoed with its code. Reading then ends, as a network loss, so the Will is
  published. Ping is answered pong. Once a close is sent the broker's writes are dropped; when
  the broker is done, close `1000` follows unless a close was sent.

The command line (BL-501) builds on it.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.MessageBus.UnitLibrary`
(ADR-0100 decision 3.9), `Surl.HttpMessage.UnitLibrary` and `Surl.WebSocket.UnitLibrary`
(ADR-0093 decision 7), and may also reference the horizontal libraries ADR-0093 decision 0
names - never another protocol server, original or extended, and never `Surl.Protocol.Mqtt`. Never construct a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the listener seam.
