# Mqtt5 requirement matrix

The server-side requirements `Surl.Extended.Protocol.Mqtt5` answers, for the `mqtt5`, `mqtt5s` and
`mqtt5+ws` schemes (ADR-0083 decision 9). Written from OASIS MQTT Version 5.0 (7 March 2019), the
specification
[ADR-0093](../../Planning/Decisions/ADR-0093-how-the-mqtt-5-broker-answers-mqtt5-mqtt5s-and-mqtt5-ws.md)
decides by, never from the Curl port's client matrices. Each row binds a server; "Covered by" names
the task that proves it, until that task replaces its ID with the named test, or says why the row
needs no test.

A row's ID is the specification's own `[MQTT-x.y.z-n]` statement. A statement that binds only a
client is left out, except where the broker must detect its breach; such a row says what the broker
does. A binding rule the specification states without a number has an ID `MQTT5-<section>-<n>` of
its own. RFC 6455 rules the WebSocket transport relies on have `RFC6455-<section>-<n>` IDs.

## Data representation, fixed header and properties (1.5, 2.1, 2.2)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Mqtt5:MQTT-1.5.4-1 | MUST NOT | A UTF-8 Encoded String holds no encoding of U+D800 to U+DFFF; a received one is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-1.5.4-2 | MUST NOT | A UTF-8 Encoded String holds no U+0000; a received one is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-1.5.4-3 | MUST NOT | A receiver does not skip or strip the sequence 0xEF 0xBB 0xBF (U+FEFF) anywhere in a string. | BL-495 |
| Mqtt5:MQTT5-1.5.4-1 | MUST | Ill-formed UTF-8 in a received packet is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-1.5.5-1 | MUST | A Variable Byte Integer the broker writes uses the fewest bytes. | BL-495 |
| Mqtt5:MQTT5-1.5.5-1 | MUST | A Variable Byte Integer of more than four bytes is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-1.5.7-1 | MUST | Both strings of a UTF-8 String Pair follow the UTF-8 Encoded String rules. | BL-495 |
| Mqtt5:MQTT-2.1.3-1 | MUST | Reserved fixed-header flag bits have the values listed; a packet whose do not is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-2.2.1-2 | MUST NOT | A QoS 0 PUBLISH carries no Packet Identifier. | BL-495 |
| Mqtt5:MQTT-2.2.1-4 | MUST | Each new QoS 1 or 2 PUBLISH the broker sends has a non-zero Packet Identifier unused among its in-flight PUBLISHes to that client. | BL-497 |
| Mqtt5:MQTT-2.2.1-5 | MUST | A PUBACK, PUBREC, PUBREL or PUBCOMP carries the Packet Identifier of the PUBLISH it answers. | BL-497 |
| Mqtt5:MQTT-2.2.1-6 | MUST | A SUBACK or UNSUBACK carries the Packet Identifier of its SUBSCRIBE or UNSUBSCRIBE. | BL-498 |
| Mqtt5:MQTT-2.2.2-1 | MUST | A packet with no properties carries a Property Length of zero. | BL-495 |
| Mqtt5:MQTT5-2.2.2.2-1 | MUST | A property appearing twice where table 2-4 allows it once, or in a packet it is not allowed in, is a Protocol Error (`0x82`). | BL-495 |

## CONNECT, CONNACK, keep alive and the Will (3.1, 3.2, 3.12)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Mqtt5:MQTT-3.1.0-1 | MUST | The first packet from a client is CONNECT; the broker closes a connection whose first packet is not, with no bytes. | BL-496 |
| Mqtt5:MQTT-3.1.0-2 | MUST | A second CONNECT is a Protocol Error and closes the connection (DISCONNECT `0x82`). | BL-496 |
| Mqtt5:MQTT-3.1.2-1 | MAY, MUST | A protocol name other than `MQTT` is not accepted and the connection is closed; the broker sends no CONNACK (ADR-0093 decision 1). | BL-496 |
| Mqtt5:MQTT-3.1.2-2 | MAY, MUST | A Protocol Version other than 5 is answered CONNACK `0x84` Unsupported Protocol Version and closed. | BL-496 |
| Mqtt5:MQTT-3.1.2-3 | MUST | The reserved CONNECT flag is 0; otherwise Malformed Packet. | BL-496 |
| Mqtt5:MQTT-3.1.2-7 | MUST | With the Will Flag 1, the Will Message is stored and associated with the session. | BL-496 |
| Mqtt5:MQTT-3.1.2-8 | MUST | The Will is published after the connection closes once its Will Delay Interval passes or the session ends, unless DISCONNECT `0x00` or a new connection for the identifier came first. | BL-496, BL-499 |
| Mqtt5:MQTT-3.1.2-9 | MUST | With the Will Flag 1, Will Properties, Will Topic and Will Payload are present. | BL-495 |
| Mqtt5:MQTT-3.1.2-10 | MUST | The Will is removed from the session once published or on DISCONNECT `0x00`. | BL-499 |
| Mqtt5:MQTT-3.1.2-11 | MUST | With the Will Flag 0, Will QoS is 0; otherwise Malformed Packet. | BL-496 |
| Mqtt5:MQTT-3.1.2-12 | MUST | With the Will Flag 1, Will QoS is 0, 1 or 2; 3 is a Malformed Packet. | BL-496 |
| Mqtt5:MQTT-3.1.2-13 | MUST | With the Will Flag 0, Will Retain is 0; otherwise Malformed Packet. | BL-496 |
| Mqtt5:MQTT-3.1.2-14 | MUST | A Will with Will Retain 0 is published as a non-retained message. | BL-496 |
| Mqtt5:MQTT-3.1.2-15 | MUST | A Will with Will Retain 1 is published as a retained message. | BL-496 |
| Mqtt5:MQTT-3.1.2-16 | MUST NOT | With the User Name Flag 0, no User Name is present. | BL-495 |
| Mqtt5:MQTT-3.1.2-17 | MUST | With the User Name Flag 1, a User Name is present. | BL-495 |
| Mqtt5:MQTT-3.1.2-18 | MUST NOT | With the Password Flag 0, no Password is present. | BL-495 |
| Mqtt5:MQTT-3.1.2-19 | MUST | With the Password Flag 1, a Password is present. | BL-495 |
| Mqtt5:MQTT-3.1.2-22 | MUST | With a non-zero Keep Alive, a connection silent for one and a half times it is closed (the broker sends DISCONNECT `0x8D` first and publishes the Will). | BL-496 |
| Mqtt5:MQTT-3.1.2-24 | MUST NOT | The broker sends no packet larger than the client's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT-3.1.2-25 | MUST | A PUBLISH too large for the client is discarded unsent and treated as delivered. | BL-497 |
| Mqtt5:MQTT-3.1.2-26 | MUST NOT | The broker sends no Topic Alias above the client's Topic Alias Maximum. | BL-497 |
| Mqtt5:MQTT-3.1.2-27 | MUST NOT | With the client's Topic Alias Maximum absent or 0, the broker sends no Topic Alias. | BL-497 |
| Mqtt5:MQTT-3.1.2-28 | MUST NOT | With Request Response Information 0, no Response Information is returned (the broker never returns it). | BL-496 |
| Mqtt5:MQTT-3.1.2-29 | MUST NOT | With Request Problem Information 0, no Reason String or User Property goes on any packet but PUBLISH, CONNACK and DISCONNECT. | BL-496, BL-499 |
| Mqtt5:MQTT-3.1.2-30 | MUST NOT | A client that set an Authentication Method sends only AUTH or DISCONNECT before CONNACK; anything else is a Protocol Error. | BL-499 |
| Mqtt5:MQTT-3.1.3-1 | MUST | The payload fields come in the order Client Identifier, Will Properties, Will Topic, Will Payload, User Name, Password. | BL-495 |
| Mqtt5:MQTT-3.1.3-2 | MUST | The Client Identifier identifies the session state the broker holds. | BL-499 |
| Mqtt5:MQTT-3.1.3-3 | MUST | The Client Identifier is present and first in the payload. | BL-495 |
| Mqtt5:MQTT-3.1.3-4 | MUST | The Client Identifier is a UTF-8 Encoded String. | BL-495 |
| Mqtt5:MQTT-3.1.3-5 | MUST | Client Identifiers of 1 to 23 alphanumeric characters are allowed (the broker allows any valid string of 1 to 65535 bytes). | BL-496 |
| Mqtt5:MQTT-3.1.3-6 | MAY, MUST | A zero-length Client Identifier is allowed and replaced by a unique one. | BL-496 |
| Mqtt5:MQTT-3.1.3-7 | MUST | The CONNECT is then handled as if it carried that identifier, which CONNACK returns as Assigned Client Identifier. | BL-496 |
| Mqtt5:MQTT-3.1.3-8 | MAY, MUST | A rejected Client Identifier gets CONNACK `0x85` and a close. | n/a: the broker rejects no Client Identifier (ADR-0093 decision 1) |
| Mqtt5:MQTT-3.1.3-9 | MUST NOT | A new connection to the session before the Will Delay Interval passes stops the Will being sent. | BL-499 |
| Mqtt5:MQTT-3.1.3-10 | MUST | The Will's User Properties keep their order when it is published. | BL-496 |
| Mqtt5:MQTT-3.1.3-11 | MUST | The Will Topic is a UTF-8 Encoded String. | BL-495 |
| Mqtt5:MQTT-3.1.3-12 | MUST | The User Name is a UTF-8 Encoded String. | BL-495 |
| Mqtt5:MQTT-3.1.4-1 | MUST | A CONNECT that does not match section 3.1's format closes the connection. | BL-496 |
| Mqtt5:MQTT-3.1.4-2 | MUST | A CONNECT failing the broker's further checks, authentication included, closes the connection (after the CONNACK of ADR-0093 decision 2). | BL-496 |
| Mqtt5:MQTT-3.1.4-3 | MUST | A CONNECT for an identifier already connected sends the older connection DISCONNECT `0x8E` and closes it. | BL-499 |
| Mqtt5:MQTT-3.1.4-4 | MUST | Clean Start is processed as section 3.1.2.4 says. | BL-499 |
| Mqtt5:MQTT-3.1.4-5 | MUST | An accepted CONNECT is acknowledged with CONNACK `0x00`. | BL-496 |
| Mqtt5:MQTT-3.1.4-6 | MUST NOT | After rejecting a CONNECT, the broker processes nothing the client sent after it except AUTH. | BL-496 |
| Mqtt5:MQTT-3.2.0-1 | MUST | CONNACK `0x00` is sent before any packet other than AUTH. | BL-496 |
| Mqtt5:MQTT-3.2.0-2 | MUST NOT | At most one CONNACK is sent on a connection. | BL-496 |
| Mqtt5:MQTT-3.2.2-1 | MUST | CONNACK's Connect Acknowledge Flags bits 7 to 1 are 0. | BL-495 |
| Mqtt5:MQTT-3.2.2-2 | MUST | Accepting Clean Start 1 sets Session Present 0. | BL-496, BL-499 |
| Mqtt5:MQTT-3.2.2-3 | MUST | Accepting Clean Start 0 sets Session Present 1 when a session was resumed and 0 otherwise, with reason code `0x00`. | BL-499 |
| Mqtt5:MQTT-3.2.2-6 | MUST | A CONNACK with a non-zero reason code has Session Present 0. | BL-496 |
| Mqtt5:MQTT-3.2.2-7 | MUST | A CONNACK with a reason code of `0x80` or above is followed by a close. | BL-496 |
| Mqtt5:MQTT-3.2.2-8 | MUST | A CONNACK uses one of the Connect Reason Codes. | BL-496 |
| Mqtt5:MQTT-3.2.2-9 | MUST | A server without QoS 1 or 2 sends Maximum QoS. | n/a: the broker serves QoS 2 and sends no Maximum QoS (ADR-0093 decision 1) |
| Mqtt5:MQTT-3.2.2-10 | MUST | A server without QoS 1 or 2 still accepts SUBSCRIBE at Requested QoS 0, 1 or 2. | n/a: the broker serves QoS 2 (ADR-0093 decision 1) |
| Mqtt5:MQTT-3.2.2-12 | SHOULD, MUST | A PUBLISH above the Maximum QoS is answered `0x9B` and closed. | n/a: the Maximum QoS is 2, so no QoS exceeds it |
| Mqtt5:MQTT-3.2.2-13 | SHOULD, MUST | A retained PUBLISH to a server without retain is answered `0x9A` and closed. | n/a: Retain Available is 1 (ADR-0093 decision 1) |
| Mqtt5:MQTT-3.2.2-15 | MUST NOT | A client sends no packet above the broker's Maximum Packet Size; the broker answers one with `0x95` Packet too large and closes, its body unread. | BL-495, BL-496 |
| Mqtt5:MQTT-3.2.2-16 | MUST | An Assigned Client Identifier is used by no other current session. | BL-496 |
| Mqtt5:MQTT-3.2.2-17 | MUST NOT | A client sends no Topic Alias above the broker's Topic Alias Maximum; the broker answers one with DISCONNECT `0x94`. | BL-497 |
| Mqtt5:MQTT-3.2.2-18 | MUST NOT | With the broker's Topic Alias Maximum absent or 0 a client sends no alias. | n/a: the broker always returns Topic Alias Maximum 10 (ADR-0093 decision 1), and an alias above it is MQTT-3.2.2-17's row |
| Mqtt5:MQTT-3.2.2-19 | MUST NOT | CONNACK carries no Reason String that would take it past the client's Maximum Packet Size. | BL-496 |
| Mqtt5:MQTT-3.2.2-20 | MUST NOT | CONNACK carries no User Property that would take it past the client's Maximum Packet Size. | BL-496 |
| Mqtt5:MQTT-3.2.2-22 | MUST | Without a Server Keep Alive in CONNACK, the broker uses the client's Keep Alive. | BL-496 |
| Mqtt5:MQTT-3.12.4-1 | MUST | PINGREQ is answered PINGRESP. | BL-496 |

## PUBLISH, its acknowledgements, QoS and flow control (3.3 to 3.7, 4.3 to 4.6, 4.9)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Mqtt5:MQTT-3.3.1-1 | MUST | A re-delivered PUBLISH has DUP 1. | BL-499 |
| Mqtt5:MQTT-3.3.1-2 | MUST | A QoS 0 PUBLISH has DUP 0. | BL-497 |
| Mqtt5:MQTT-3.3.1-3 | MUST | An outgoing PUBLISH's DUP depends only on whether it is a retransmission. | BL-497 |
| Mqtt5:MQTT-3.3.1-4 | MUST NOT | A PUBLISH with both QoS bits set is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-3.3.1-5 | MUST | A PUBLISH with RETAIN 1 replaces the topic's retained message. | BL-497 |
| Mqtt5:MQTT-3.3.1-6 | MUST | A retained PUBLISH with an empty payload removes the topic's retained message. | BL-497 |
| Mqtt5:MQTT-3.3.1-7 | MUST NOT | A retained message with an empty payload is not stored. | BL-497 |
| Mqtt5:MQTT-3.3.1-8 | MUST NOT | A PUBLISH with RETAIN 0 is not stored and changes no retained message. | BL-497 |
| Mqtt5:MQTT-3.3.2-1 | MUST | The Topic Name is a UTF-8 Encoded String. | BL-495 |
| Mqtt5:MQTT-3.3.2-2 | MUST NOT | A Topic Name holds no wildcard; one that does is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-3.3.2-4 | MUST | The Payload Format Indicator is forwarded unaltered. | BL-497 |
| Mqtt5:MQTT5-3.3.2.3.2-1 | MAY | A PUBLISH whose payload is not valid UTF-8 under Payload Format Indicator 1 is answered `0x99` Payload format invalid and not delivered (ADR-0093 decision 4). | BL-497 |
| Mqtt5:MQTT-3.3.2-5 | MUST | A message whose Message Expiry Interval passes before onward delivery starts is deleted for that subscriber. | BL-497 |
| Mqtt5:MQTT-3.3.2-6 | MUST | A forwarded message carries the Message Expiry Interval less the time it waited. | BL-497 |
| Mqtt5:MQTT-3.3.2-7 | MUST NOT | Topic Alias mappings do not carry from one connection to another. | BL-497 |
| Mqtt5:MQTT-3.3.2-8 | MUST NOT | No PUBLISH carries Topic Alias 0; the broker sends none and answers a client's with DISCONNECT `0x94`. | BL-497 |
| Mqtt5:MQTT-3.3.2-11 | MUST NOT | The broker sends no Topic Alias above the client's Topic Alias Maximum. | BL-497 |
| Mqtt5:MQTT-3.3.2-12 | MUST | The broker accepts every Topic Alias from 1 to the Topic Alias Maximum it returned. | BL-497 |
| Mqtt5:MQTT-3.3.2-13 | MUST | The Response Topic is a UTF-8 Encoded String. | BL-495 |
| Mqtt5:MQTT-3.3.2-14 | MUST NOT | The Response Topic holds no wildcard; one that does is a Protocol Error. | BL-497 |
| Mqtt5:MQTT-3.3.2-15 | MUST | The Response Topic is forwarded unaltered. | BL-497 |
| Mqtt5:MQTT-3.3.2-16 | MUST | The Correlation Data is forwarded unaltered. | BL-497 |
| Mqtt5:MQTT-3.3.2-17 | MUST | Every User Property is forwarded unaltered. | BL-497 |
| Mqtt5:MQTT-3.3.2-18 | MUST | User Properties keep their order when forwarded. | BL-497 |
| Mqtt5:MQTT-3.3.2-19 | MUST | The Content Type is a UTF-8 Encoded String. | BL-495 |
| Mqtt5:MQTT-3.3.2-20 | MUST | The Content Type is forwarded unaltered. | BL-497 |
| Mqtt5:MQTT-3.3.4-1 | MUST | A PUBLISH is answered as its QoS says. | BL-497 |
| Mqtt5:MQTT-3.3.4-6 | MUST NOT | A client PUBLISH carries no Subscription Identifier; one that does is a Protocol Error. | BL-497 |
| Mqtt5:MQTT-3.3.4-7 | MUST NOT | A client keeps no more unacknowledged QoS 1 and 2 PUBLISHes than the broker's Receive Maximum; one past it is answered DISCONNECT `0x93`. | BL-497 |
| Mqtt5:MQTT-3.3.4-9 | MUST NOT | The broker keeps no more unacknowledged QoS 1 and 2 PUBLISHes to a client than its Receive Maximum. | BL-497 |
| Mqtt5:MQTT-3.3.4-10 | MUST NOT | The broker delays no packet other than PUBLISH because its Receive Maximum is reached. | BL-497 |
| Mqtt5:MQTT5-3.4.2.1-1 | MAY | A PUBLISH no subscription matched is acknowledged `0x10` No matching subscribers. | BL-497 |
| Mqtt5:MQTT-3.4.2-1 | MUST | A PUBACK uses one of the PUBACK Reason Codes. | BL-497 |
| Mqtt5:MQTT-3.4.2-2 | MUST NOT | A PUBACK carries no Reason String that would take it past the receiver's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT-3.4.2-3 | MUST NOT | A PUBACK carries no User Property that would take it past the receiver's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT-3.5.2-1 | MUST | A PUBREC uses one of the PUBREC Reason Codes. | BL-497 |
| Mqtt5:MQTT-3.5.2-2 | MUST NOT | A PUBREC carries no Reason String that would take it past the receiver's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT-3.5.2-3 | MUST NOT | A PUBREC carries no User Property that would take it past the receiver's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT-3.6.1-1 | MUST | PUBREL's fixed-header flags are 0010; anything else is a Malformed Packet and closes the connection. | BL-495 |
| Mqtt5:MQTT-3.6.2-1 | MUST | A PUBREL uses one of the PUBREL Reason Codes. | BL-497 |
| Mqtt5:MQTT-3.6.2-2 | MUST NOT | A PUBREL carries no Reason String that would take it past the receiver's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT-3.6.2-3 | MUST NOT | A PUBREL carries no User Property that would take it past the receiver's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT5-3.7.2.1-1 | MUST | A PUBREL for a Packet Identifier the broker does not hold is answered PUBCOMP `0x92` Packet Identifier not found. | BL-497 |
| Mqtt5:MQTT-3.7.2-1 | MUST | A PUBCOMP uses one of the PUBCOMP Reason Codes. | BL-497 |
| Mqtt5:MQTT-3.7.2-2 | MUST NOT | A PUBCOMP carries no Reason String that would take it past the receiver's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT-3.7.2-3 | MUST NOT | A PUBCOMP carries no User Property that would take it past the receiver's Maximum Packet Size. | BL-497 |
| Mqtt5:MQTT-4.3.1-1 | MUST | A QoS 0 PUBLISH the broker sends has QoS 0 and DUP 0. | BL-497 |
| Mqtt5:MQTT-4.3.2-1 | MUST | A QoS 1 sender assigns an unused Packet Identifier to each new message. | BL-497 |
| Mqtt5:MQTT-4.3.2-2 | MUST | A QoS 1 sender sends the PUBLISH with QoS 1 and DUP 0. | BL-497 |
| Mqtt5:MQTT-4.3.2-3 | MUST | A QoS 1 sender treats the PUBLISH as unacknowledged until its PUBACK. | BL-497 |
| Mqtt5:MQTT-4.3.2-4 | MUST | A QoS 1 receiver answers PUBACK with the PUBLISH's Packet Identifier once it owns the message. | BL-497 |
| Mqtt5:MQTT-4.3.2-5 | MUST | After its PUBACK, a PUBLISH with the same Packet Identifier is a new message. | BL-497 |
| Mqtt5:MQTT-4.3.3-1 | MUST | A QoS 2 sender assigns an unused Packet Identifier to each new message. | BL-497 |
| Mqtt5:MQTT-4.3.3-2 | MUST | A QoS 2 sender sends the PUBLISH with QoS 2 and DUP 0. | BL-497 |
| Mqtt5:MQTT-4.3.3-3 | MUST | A QoS 2 sender treats the PUBLISH as unacknowledged until its PUBREC. | BL-497 |
| Mqtt5:MQTT-4.3.3-4 | MUST | The PUBREL carries the original PUBLISH's Packet Identifier. | BL-497 |
| Mqtt5:MQTT-4.3.3-5 | MUST | A QoS 2 sender treats the PUBREL as unacknowledged until its PUBCOMP. | BL-497 |
| Mqtt5:MQTT-4.3.3-6 | MUST NOT | A QoS 2 sender does not re-send the PUBLISH after its PUBREL. | BL-497 |
| Mqtt5:MQTT-4.3.3-7 | MUST NOT | Message expiry is not applied once the QoS 2 PUBLISH has been sent. | BL-497 |
| Mqtt5:MQTT-4.3.3-8 | MUST | A QoS 2 receiver answers PUBREC with the PUBLISH's Packet Identifier once it owns the message. | BL-497 |
| Mqtt5:MQTT-4.3.3-9 | MUST | After a PUBREC of `0x80` or above, a PUBLISH with that Packet Identifier is a new message. | BL-497 |
| Mqtt5:MQTT-4.3.3-10 | MUST NOT | A repeated QoS 2 PUBLISH before PUBREL is not delivered onward twice. | BL-497 |
| Mqtt5:MQTT-4.3.3-11 | MUST | PUBREL is answered PUBCOMP with the same Packet Identifier. | BL-497 |
| Mqtt5:MQTT-4.3.3-12 | MUST | After its PUBCOMP, a PUBLISH with that Packet Identifier is a new message. | BL-497 |
| Mqtt5:MQTT-4.3.3-13 | MUST | The QoS 2 acknowledgement sequence continues even when message expiry was applied. | BL-497 |
| Mqtt5:MQTT-4.4.0-1 | MUST NOT | Unacknowledged PUBLISH and PUBREL packets are re-sent only when a session is resumed, never at another time. | BL-499 |
| Mqtt5:MQTT-4.4.0-2 | MUST NOT | A PUBLISH whose PUBACK or PUBREC carried `0x80` or above is acknowledged and not re-sent. | BL-497 |
| Mqtt5:MQTT-4.5.0-1 | MUST | A message the broker takes is added to the session state of every client with a matching subscription. | BL-497 |
| Mqtt5:MQTT-4.6.0-5 | MUST | Messages on an Ordered Topic go to each subscriber, for one topic and QoS, in the order received from any one client. | BL-497 |
| Mqtt5:MQTT-4.6.0-6 | MUST | By default every topic is an Ordered Topic for non-shared subscriptions. | BL-497 |
| Mqtt5:MQTT-4.9.0-1 | MUST | The broker's initial send quota is non-zero and at most the client's Receive Maximum. | BL-497 |
| Mqtt5:MQTT-4.9.0-2 | MUST NOT | With the send quota at zero, the broker sends no QoS 1 or 2 PUBLISH. | BL-497 |
| Mqtt5:MQTT-4.9.0-3 | MUST | The broker keeps answering every other packet while its quota is zero. | BL-497 |

## SUBSCRIBE, UNSUBSCRIBE, topics and shared subscriptions (3.8 to 3.11, 4.7, 4.8)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Mqtt5:MQTT-3.3.1-9 | MUST | Retain Handling 0 sends the retained messages matching the filter at subscribe time. | BL-498 |
| Mqtt5:MQTT-3.3.1-10 | MUST | Retain Handling 1 sends them only when the subscription did not exist, and not otherwise. | BL-498 |
| Mqtt5:MQTT-3.3.1-11 | MUST NOT | Retain Handling 2 sends no retained message. | BL-498 |
| Mqtt5:MQTT-3.3.1-12 | MUST | Retain As Published 0 forwards with RETAIN 0. | BL-498 |
| Mqtt5:MQTT-3.3.1-13 | MUST | Retain As Published 1 forwards with the RETAIN flag received. | BL-498 |
| Mqtt5:MQTT-3.3.2-3 | MUST | A forwarded PUBLISH's Topic Name matches the subscription's filter by section 4.7. | BL-498 |
| Mqtt5:MQTT-3.3.4-2 | MUST | A message matching several of a client's subscriptions is delivered at the highest QoS among them. | BL-498 |
| Mqtt5:MQTT-3.3.4-3 | MUST | The Subscription Identifiers of the matching subscriptions are sent with the message. | BL-498 |
| Mqtt5:MQTT-3.3.4-4 | MUST | A single copy carries every matching subscription's Subscription Identifier. | BL-498 |
| Mqtt5:MQTT-3.3.4-5 | MUST | Separate copies each carry their own subscription's Subscription Identifier (the broker sends one copy, ADR-0093 decision 5). | BL-498 |
| Mqtt5:MQTT5-3.8.2.1.2-1 | MUST | A Subscription Identifier of 0 is a Protocol Error. | BL-498 |
| Mqtt5:MQTT-3.8.1-1 | MUST | SUBSCRIBE's fixed-header flags are 0010; anything else is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-3.8.3-1 | MUST | Topic Filters are UTF-8 Encoded Strings. | BL-495 |
| Mqtt5:MQTT-3.8.3-2 | MUST | A SUBSCRIBE holds at least one filter and options pair; an empty one is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-3.8.3-3 | MUST NOT | With No Local 1, a message is not forwarded to the connection whose Client Identifier published it. | BL-498 |
| Mqtt5:MQTT-3.8.3-4 | MUST | No Local 1 on a shared subscription is a Protocol Error. | BL-498 |
| Mqtt5:MQTT-3.8.3-5 | MUST | Non-zero reserved bits in the subscription options are a Malformed Packet. | BL-495, BL-498 |
| Mqtt5:MQTT-3.8.4-1 | MUST | SUBSCRIBE is answered SUBACK. | BL-498 |
| Mqtt5:MQTT-3.8.4-2 | MUST | The SUBACK carries the SUBSCRIBE's Packet Identifier. | BL-498 |
| Mqtt5:MQTT-3.8.4-3 | MUST | A filter identical to a non-shared subscription of the session replaces it. | BL-498 |
| Mqtt5:MQTT-3.8.4-4 | MUST | On replacement with Retain Handling 0 the retained messages are re-sent, and no message is lost. | BL-498 |
| Mqtt5:MQTT-3.8.4-5 | MUST | Several filters in one SUBSCRIBE are handled as a sequence of SUBSCRIBEs with one SUBACK. | BL-498 |
| Mqtt5:MQTT-3.8.4-6 | MUST | The SUBACK has a reason code for each filter. | BL-498 |
| Mqtt5:MQTT-3.8.4-7 | MUST | Each reason code is the granted QoS or a failure. | BL-498 |
| Mqtt5:MQTT-3.8.4-8 | MUST | A message is delivered at the lower of its published QoS and the granted QoS. | BL-498 |
| Mqtt5:MQTT-3.9.2-1 | MUST NOT | A SUBACK carries no Reason String that would take it past the client's Maximum Packet Size. | BL-498 |
| Mqtt5:MQTT-3.9.2-2 | MUST NOT | A SUBACK carries no User Property that would take it past the client's Maximum Packet Size. | BL-498 |
| Mqtt5:MQTT-3.9.3-1 | MUST | SUBACK reason codes follow the SUBSCRIBE's filter order. | BL-498 |
| Mqtt5:MQTT-3.9.3-2 | MUST | A SUBACK uses one of the Subscribe Reason Codes for each filter. | BL-498 |
| Mqtt5:MQTT-3.10.1-1 | MUST | UNSUBSCRIBE's fixed-header flags are 0010; anything else is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-3.10.3-1 | MUST | UNSUBSCRIBE's filters are UTF-8 Encoded Strings. | BL-495 |
| Mqtt5:MQTT-3.10.3-2 | MUST | An UNSUBSCRIBE holds at least one filter; an empty one is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-3.10.4-1 | MUST | A filter matching a subscription exactly deletes it. | BL-498 |
| Mqtt5:MQTT-3.10.4-2 | MUST | After UNSUBSCRIBE no new message for those filters is queued for the client. | BL-498 |
| Mqtt5:MQTT-3.10.4-3 | MUST | QoS 1 and 2 deliveries already started complete. | BL-498 |
| Mqtt5:MQTT-3.10.4-4 | MUST | UNSUBSCRIBE is answered UNSUBACK. | BL-498 |
| Mqtt5:MQTT-3.10.4-5 | MUST | UNSUBACK is sent even when nothing was deleted (`0x11` No subscription existed). | BL-498 |
| Mqtt5:MQTT-3.10.4-6 | MUST | Several filters in one UNSUBSCRIBE are handled as a sequence with one UNSUBACK. | BL-498 |
| Mqtt5:MQTT-3.11.2-1 | MUST NOT | An UNSUBACK carries no Reason String that would take it past the client's Maximum Packet Size. | BL-498 |
| Mqtt5:MQTT-3.11.2-2 | MUST NOT | An UNSUBACK carries no User Property that would take it past the client's Maximum Packet Size. | BL-498 |
| Mqtt5:MQTT-3.11.3-1 | MUST | UNSUBACK reason codes follow the UNSUBSCRIBE's filter order. | BL-498 |
| Mqtt5:MQTT-3.11.3-2 | MUST | An UNSUBACK uses one of the Unsubscribe Reason Codes for each filter. | BL-498 |
| Mqtt5:MQTT-4.7.0-1 | MUST NOT | Wildcards appear in Topic Filters only, never in a Topic Name. | BL-495 |
| Mqtt5:MQTT-4.7.1-1 | MUST | `#` is the filter's last character; a filter breaking it gets `0x8F`. | BL-498 |
| Mqtt5:MQTT-4.7.1-2 | MUST | `#` and `+` occupy a whole level; a filter breaking it gets `0x8F`. | BL-498 |
| Mqtt5:MQTT-4.7.2-1 | MUST NOT | A filter starting with `#` or `+` does not match a Topic Name starting with `$`. | BL-498 |
| Mqtt5:MQTT-4.7.3-1 | MUST | Topic Names and Topic Filters are at least one character long. | BL-498 |
| Mqtt5:MQTT-4.7.3-2 | MUST NOT | Topic Names and Topic Filters hold no U+0000. | BL-495, BL-498 |
| Mqtt5:MQTT-4.7.3-3 | MUST NOT | Topic Names and Topic Filters encode to at most 65535 bytes. | BL-495 |
| Mqtt5:MQTT-4.7.3-4 | MUST NOT | Matching does no normalisation or substitution of characters. | BL-498 |
| Mqtt5:MQTT-4.8.2-1 | MUST | A shared subscription's filter starts `$share/` and has a ShareName of at least one character. | BL-498 |
| Mqtt5:MQTT-4.8.2-2 | MUST | The `/` after the ShareName is followed by a Topic Filter. | BL-498 |
| Mqtt5:MQTT-4.8.2-3 | MUST | A shared delivery respects the chosen member's granted QoS. | BL-498 |
| Mqtt5:MQTT-4.8.2-4 | MUST | A QoS 2 shared delivery cut by a lost connection is completed with that member when it reconnects. | BL-498 |
| Mqtt5:MQTT-4.8.2-5 | MUST NOT | If that member's session ends first, the message goes to no other member. | BL-498 |
| Mqtt5:MQTT-4.8.2-6 | MUST | A member's PUBACK or PUBREC of `0x80` or above discards the message, sent to no other member. | BL-498 |
| Mqtt5:MQTT5-4.8.2-1 | MUST | Each message matching a shared subscription goes to one member session only, and retained messages are not sent for it. | BL-498 |

## Sessions, DISCONNECT, AUTH and errors (3.1.2.4, 3.14, 3.15, 4.1, 4.12, 4.13)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Mqtt5:MQTT-3.1.2-4 | MUST | Clean Start 1 discards any existing session and starts a new one. | BL-499 |
| Mqtt5:MQTT-3.1.2-5 | MUST | Clean Start 0 with a session for the identifier resumes it. | BL-499 |
| Mqtt5:MQTT-3.1.2-6 | MUST | Clean Start 0 with no session creates a new one. | BL-499 |
| Mqtt5:MQTT-3.1.2-23 | MUST | A session with a Session Expiry Interval above 0 is kept after its connection closes. | BL-499 |
| Mqtt5:MQTT5-3.1.2.11.2-1 | MUST | A DISCONNECT setting a non-zero Session Expiry Interval after CONNECT set 0 is a Protocol Error. | BL-499 |
| Mqtt5:MQTT-4.1.0-1 | MUST NOT | Session state is not discarded while its connection is open. | BL-499 |
| Mqtt5:MQTT-4.1.0-2 | MUST | Session state is discarded once its connection is closed and the Session Expiry Interval has passed. | BL-499 |
| Mqtt5:MQTT-3.14.0-1 | MUST NOT | The broker sends no DISCONNECT before a CONNACK below `0x80`. | BL-499 |
| Mqtt5:MQTT-3.14.1-1 | MUST | DISCONNECT's fixed-header flags are 0; otherwise DISCONNECT `0x81`. | BL-495 |
| Mqtt5:MQTT-3.14.2-1 | MUST | A DISCONNECT uses one of the DISCONNECT Reason Codes. | BL-499 |
| Mqtt5:MQTT-3.14.2-2 | MUST NOT | The broker's DISCONNECT carries no Session Expiry Interval. | BL-499 |
| Mqtt5:MQTT-3.14.2-3 | MUST NOT | A DISCONNECT carries no Reason String that would take it past the receiver's Maximum Packet Size. | BL-499 |
| Mqtt5:MQTT-3.14.2-4 | MUST NOT | A DISCONNECT carries no User Property that would take it past the receiver's Maximum Packet Size. | BL-499 |
| Mqtt5:MQTT-3.14.4-1 | MUST NOT | After sending DISCONNECT the broker sends nothing more on the connection. | BL-499 |
| Mqtt5:MQTT-3.14.4-2 | MUST | After sending DISCONNECT the broker closes the connection. | BL-499 |
| Mqtt5:MQTT-3.14.4-3 | MUST | A client DISCONNECT `0x00` discards the Will unpublished. | BL-499 |
| Mqtt5:MQTT5-3.14.2.1-1 | MUST | A client DISCONNECT `0x04` Disconnect with Will Message publishes the Will. | BL-499 |
| Mqtt5:MQTT5-3.14.2.1-2 | MAY | The broker sends DISCONNECT `0x8B` Server shutting down when `surl` stops. | BL-499 |
| Mqtt5:MQTT-3.15.1-1 | MUST | AUTH's fixed-header flags are 0; anything else is a Malformed Packet. | BL-495 |
| Mqtt5:MQTT-3.15.2-1 | MUST | An AUTH uses one of the Authenticate Reason Codes. | BL-499 |
| Mqtt5:MQTT-3.15.2-2 | MUST NOT | An AUTH carries no Reason String that would take it past the receiver's Maximum Packet Size. | BL-499 |
| Mqtt5:MQTT-3.15.2-3 | MUST NOT | An AUTH carries no User Property that would take it past the receiver's Maximum Packet Size. | BL-499 |
| Mqtt5:MQTT-4.12.0-1 | MAY, MUST | An Authentication Method the broker does not serve is answered CONNACK `0x8C` and closed. | BL-499 |
| Mqtt5:MQTT-4.12.0-2 | MUST | An AUTH the broker sends for more data carries `0x18` Continue authentication. | BL-499 |
| Mqtt5:MQTT-4.12.0-3 | MUST | A client's AUTH in answer carries `0x18`; another reason code is a Protocol Error. | BL-499 |
| Mqtt5:MQTT-4.12.0-4 | MAY, MUST | A rejected authentication is answered CONNACK `0x87` and closed. | BL-499 |
| Mqtt5:MQTT-4.12.0-5 | MUST | Every AUTH and the successful CONNACK carry the CONNECT's Authentication Method. | BL-499 |
| Mqtt5:MQTT-4.12.0-6 | MUST NOT | Without an Authentication Method in CONNECT, the broker sends no AUTH and no Authentication Method in CONNACK. | BL-499 |
| Mqtt5:MQTT-4.12.0-7 | MUST NOT | Without an Authentication Method in CONNECT the client sends no AUTH; one is answered DISCONNECT `0x82`. | BL-499 |
| Mqtt5:MQTT-4.12.1-1 | MUST | Re-authentication uses the connection's original Authentication Method; another is a Protocol Error. | BL-499 |
| Mqtt5:MQTT-4.12.1-2 | SHOULD, MUST | A failed re-authentication is answered DISCONNECT `0x87` and closed. | BL-499 |
| Mqtt5:MQTT-4.13.1-1 | MUST | A Malformed Packet or Protocol Error with a reason code in the specification closes the connection (after CONNACK or DISCONNECT with that code). | BL-499 |
| Mqtt5:MQTT-4.13.2-1 | MUST | A reason code of `0x80` or above closes the connection whether or not CONNACK or DISCONNECT is sent. | BL-499 |

## Transports: TCP, TLS and WebSocket (4.2, chapter 5, chapter 6, RFC 6455)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Mqtt5:MQTT-4.2.0-1 | MUST | The broker runs over an ordered, lossless byte stream both ways: TCP (`mqtt5`), TLS (`mqtt5s`) and WebSocket (`mqtt5+ws`). | BL-500 |
| Mqtt5:MQTT-6.0.0-1 | MUST | MQTT packets travel in binary frames; any other data frame closes the connection. | BL-500 |
| Mqtt5:MQTT-6.0.0-2 | MUST NOT | Packets are not assumed aligned on frame boundaries: a packet may span frames and a frame may hold several. | BL-500 |
| Mqtt5:MQTT-6.0.0-3 | MUST | The client offers the `mqtt` subprotocol; an upgrade that does not is refused `400 Bad Request` (ADR-0093 decision 7). | BL-500 |
| Mqtt5:MQTT-6.0.0-4 | MUST | The subprotocol the broker selects and returns is `mqtt`. | BL-500 |
| Mqtt5:RFC6455-4.2.2-1 | MUST | An accepted upgrade is answered `101 Switching Protocols` with `Upgrade: websocket`, `Connection: Upgrade` and the `Sec-WebSocket-Accept` computed from the key. | BL-500 |
| Mqtt5:RFC6455-4.2.2-2 | MUST | `Sec-WebSocket-Protocol` in the answer names one subprotocol the client offered. | BL-500 |
| Mqtt5:RFC6455-5.1-1 | MUST | A client frame that is not masked closes the connection; the broker's frames are not masked. | BL-500 |
| Mqtt5:RFC6455-5.5.1-1 | MUST | A client Close is answered with a Close, and the connection ends. | BL-500 |
| Mqtt5:RFC6455-5.5.2-1 | MUST | A Ping is answered with a Pong carrying its application data. | BL-500 |
