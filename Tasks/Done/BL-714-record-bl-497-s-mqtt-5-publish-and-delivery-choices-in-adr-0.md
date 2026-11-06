---
id: BL-714
title: Record BL-497's MQTT 5 PUBLISH and delivery choices in ADR-0093
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-497, BL-509]
touches: [Documentation/Planning/Decisions/ADR-0093-how-the-mqtt-5-broker-answers-mqtt5-mqtt5s-and-mqtt5-ws.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-714 — Record BL-497's MQTT 5 PUBLISH and delivery choices in ADR-0093

## Goal

ADR-0093 states the choices BL-497 made where decision 4 (as ADR-0100 decision 3.9 amends it) left a gap, each
marked "Decided by Claude under Stewart's delegation", so the ADR is true of `Surl.Extended.Protocol.Mqtt5` as built.

## Context

- BL-497 could not write the ADR itself: BL-509 held `Documentation/Planning/Decisions` while it ran. Its Notes
  list the choices; `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/CLAUDE.md` and `Mqtt5Broker`'s
  doc comment describe them as built.
- The choices to record (each with its why, from BL-497's Notes):
  1. A login an account matched publishes and subscribes as that account; one accepted unchecked
     (`--allow-anonymous`) as an anonymous caller.
  2. ADR-0093 decision 2's "never `0x87` for a topic" is superseded by ADR-0102 decision 3.4: topic access refusal
     is PUBACK/PUBREC/SUBACK `0x87`, and a refused QoS 0 PUBLISH is dropped and noted.
  3. Bus outcomes: `StoreFull` and `TooManyTopics` are `0x97`; `MessageTooLarge` is DISCONNECT `0x95` (`0x95` is
     not a PUBACK reason code); `0x10` when no subscription and no queue took the message.
  4. A Will Topic the bus cannot map (an empty one, `[MQTT-4.7.3-1]`) is CONNACK `0x81`.
  5. The Message Expiry Interval sent on delivery is the time left rounded up to whole seconds.
  6. At most 1000 deliveries wait for one client (ADR-0006's bound); past it a message is dropped for that client.
  7. A delivery larger than the client's Maximum Packet Size is dropped and noted (`[MQTT-3.1.2-25]`).
  8. A delivery on a canonical topic the client cannot name (`TopicNameLookup` says `Unreachable`) is dropped.
  9. A Will publish is not followed by `SaveChangesAsync`; the next save after any change writes it.
  10. BL-497 answers SUBSCRIBE in its plain form so deliveries have a subscriber; BL-498 completes it.

## Acceptance criteria

- [x] ADR-0093 has an amendment section listing the ten choices above, each with its reason and the marker
      "Decided by Claude under Stewart's delegation".
- [x] ADR-0093's decision 2 "Authorization" bullet and decision 4's "Never `0x87`" sentence say that ADR-0102
      decision 3.4 supersedes them.

## Notes

- Done directly rather than through `align-and-document`: one ADR file and ten listed choices. Recorded as
  ADR-0093 decision 10, an amendment section inside the Decision part (as ADR-0102 decision 3.5 does for BL-664),
  with a header **Amended:** line and supersession notes in decision 2's "Authorization" bullet and decision 4's
  "Never `0x87`". The reasons come from BL-497's Notes and the Mqtt5 library's `CLAUDE.md`.
- No `.cs` or project file changed, so the `verify` skill was not needed; a build and the fast tests ran anyway.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0093 decision 10 records BL-497's ten PUBLISH and delivery choices; decisions 2 and 4 say ADR-0102 3.4 supersedes their never-0x87
