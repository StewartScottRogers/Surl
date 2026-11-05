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
  `Mqtt5ConnectJudge` for property values and the Authentication Method, the login through
  `IAuthenticationPolicy`, CONNACK with `Mqtt5ConnectAcknowledgementProperties`, then PINGREQ,
  DISCONNECT and the Keep Alive (1.5 times the one in force, then DISCONNECT `0x8D`).
- `Mqtt5BrokerState` is shared by every connection of one broker: connected client identifiers,
  assigned `surl-<32 hex>` identifiers, Wills waiting out their delay on the context's
  `TimeProvider`, and `ApplicationMessagePublished`, through which Wills are published.
- Still to come, and answered for now as stated: an Authentication Method is CONNACK `0x8C` until
  BL-499 adds SCRAM; PUBLISH and its acknowledgements, SUBSCRIBE and UNSUBSCRIBE are DISCONNECT
  `0x83` until BL-497 and BL-498; sessions are not kept (Session Present is always 0) until BL-499;
  a CONNECT for a connected identifier does not yet take the session over (BL-678).

PUBLISH and the QoS flows (BL-497), SUBSCRIBE (BL-498), sessions, DISCONNECT and AUTH (BL-499),
`mqtt5+ws` (BL-500) and the command line (BL-501) build on it.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference the
horizontal libraries ADR-0093 decision 0 names - never another protocol server, original or
extended, and never `Surl.Protocol.Mqtt`. Never construct a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the listener seam.
