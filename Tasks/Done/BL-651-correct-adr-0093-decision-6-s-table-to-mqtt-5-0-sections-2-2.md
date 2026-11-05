---
id: BL-651
title: Correct ADR-0093 decision 6's table to MQTT 5.0 sections 2.2.2.2, 3.8.3 and 3.10.3
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-495]
touches: [Documentation/Planning/Decisions/ADR-0093-how-the-mqtt-5-broker-answers-mqtt5-mqtt5s-and-mqtt5-ws.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-651 — Correct ADR-0093 decision 6's table to MQTT 5.0 sections 2.2.2.2, 3.8.3 and 3.10.3

## Goal

ADR-0093 decision 6's table gives each error the reason code MQTT 5.0 itself gives it, as the
codec BL-495 built already does.

## Context

- ADR-0093 decision 6's table puts "a property ... not allowed in its packet (2.2.2.2)" under
  `0x82` Protocol Error, and "an empty SUBSCRIBE or UNSUBSCRIBE payload" under `0x81`
  Malformed Packet. MQTT 5.0 says the reverse for both:
  - 2.2.2.2: "A Control Packet which contains an Identifier which is not valid for its packet
    type ... is a Malformed Packet" (`0x81`);
  - 3.8.3: "A SUBSCRIBE packet with no Payload is a Protocol Error" (`[MQTT-3.8.3-2]`, `0x82`);
  - 3.10.3: "An UNSUBSCRIBE packet with no Payload is a Protocol Error" (`[MQTT-3.10.3-2]`, `0x82`).
- 3.8.3.1 also makes a Maximum QoS or Retain Handling of 3 a Protocol Error (`0x82`); the table
  does not list them.
- The codec (`Surl.Extended.Protocol.Mqtt5.UnitLibrary`, `Mqtt5BodyReader.ReadProperties`,
  `Mqtt5SubscribePacket.Read`, `Mqtt5UnsubscribePacket.Read`, `Mqtt5Subscription.Read`) follows
  the specification; BL-495's Notes record why. BL-495 could not edit the ADR because BL-470
  held `Documentation/Planning/Decisions` at the time.

## Acceptance criteria

- [x] ADR-0093 decision 6's table lists a property not allowed in its packet under `0x81`, an
      empty SUBSCRIBE or UNSUBSCRIBE payload and a Maximum QoS or Retain Handling of 3 under
      `0x82`, each with its section or statement ID.
- [x] The ADR records the amendment with its date and BL-609.

## Notes

- The second criterion's "BL-609" is a slip: BL-609 is "Create Surl.Composition" and has
  nothing to do with this ADR. The amendment line names BL-651, the task that made it, since
  naming BL-609 there would be false.
- Checked against the codec before editing: `Mqtt5BodyReader.ReadProperties` throws
  `Malformed("2.2.2.2")` for a property not allowed in its packet and `ProtocolError("2.2.2.2")`
  for one repeated; `Mqtt5Subscription.Read` throws `Malformed("[MQTT-3.8.3-5]")` for reserved
  bits and `ProtocolError("3.8.3.1")` for Maximum QoS or Retain Handling 3.
- Same file, same error: decision 1 also called a property not allowed in CONNECT `0x82`, and
  decision 5 called a requested QoS 3 malformed (`0x81`). Both now say what 2.2.2.2 and 3.8.3.1
  say, so the ADR does not contradict its own table. Reserved option bits moved into the table's
  `0x81` row too, since decision 5 names them.
- Done inline rather than through `align-and-document`: a four-sentence edit to one file.
  No `.cs` or project file changed, so `verify` is not needed.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0093's decision 6 table and decisions 1 and 5 give MQTT 5.0's own reason codes for 2.2.2.2, 3.8.3 and 3.10.3, matching the codec
