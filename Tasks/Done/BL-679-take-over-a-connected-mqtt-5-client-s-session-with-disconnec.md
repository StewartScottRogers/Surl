---
id: BL-679
title: Take over a connected MQTT 5 client's session with DISCONNECT 0x8E
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-499]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-679 — Take over a connected MQTT 5 client's session with DISCONNECT 0x8E

## Goal

When a `CONNECT` names a client identifier that is already connected, the MQTT 5 broker sends the older
connection `DISCONNECT` `0x8E` Session taken over and closes it, handling its Will as MQTT 5.0 3.1.2.5 and 3.1.4
say, as ADR-0093 decision 1 ("Session takeover") decides.

## Context

- ADR-0093 decision 1, "Session takeover"; MQTT 5.0 `[MQTT-3.1.4-3]` and the non-normative note after it on when
  the older connection's Will is sent (Will Delay 0; Session Expiry 0 or the new Clean Start 1).
- BL-496 left it out: `Mqtt5BrokerState.RegisterClient` simply records the newer connection for the identifier, and
  `UnregisterClient` leaves it registered when the older one ends; the older connection keeps running. The taken-over
  connection needs a way to be told (for example a cancellation the broker links into its packet reads) and to write
  `DISCONNECT` `0x8E` before it closes.
- Depends on BL-499, which keeps sessions, so the takeover can resume or discard the session as Clean Start says.

## Acceptance criteria

- [x] Named tests in `Surl.Extended.Protocol.Mqtt5.UnitTests` show:
      - a second connection's `CONNECT` with a connected identifier makes the first write `DISCONNECT` `0x8E`
        (`E0 01 8E`) and end, while the second gets `CONNACK` `0x00`;
      - the first connection's Will published at once when its Will Delay Interval is 0;
      - the first connection's Will published when the new `CONNECT` has Clean Start 1, and not when it has Clean
        Start 0 and a Will Delay Interval that has not passed.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Design (decided by Claude under Stewart's delegation, within ADR-0093 decision 1): a registration is now a
  `Mqtt5ClientRegistration`. `Mqtt5BrokerState.RegisterClient` for a connected identifier cancels the older
  registration's `TakenOverToken`, which the older connection's Keep Alive read is linked to; that read ending with the
  token cancelled is answered `DISCONNECT` `0x8E` (`E0 01 8E`). `EndConnection` now unregisters last, after
  `CloseSession` and the Will, and the newer connection awaits `PreviousEnded` before `OpenSession`. So the Will rules
  of 3.1.2.5 and the note after `[MQTT-3.1.4-3]` fall out of the existing close and `SettlePendingWill` paths: Will
  Delay 0 (or Session Expiry 0) published at once; a delayed Will published by a Clean Start 1 takeover and discarded
  by a Clean Start 0 one, which now resumes the older connection's session (Session Present 1).
- Default taken: the newer connection waits for the older one within its own head timeout; past it, it unregisters
  and closes with no reply, as the head timeout does elsewhere before CONNACK. A failed CONNACK write now closes the
  session and unregisters, so a takeover never waits on a connection that died before CONNACK.
- `Mqtt5StalledWriteConnection` (test fake) now hands its request over in as many reads as the reader's buffers need.
- Tests: `Mqtt5BrokerTests.Takeover.cs` (6 methods, 8 cases); the BL-499 test that pinned "a held session is not
  resumed" is replaced by `ServeAsync_TakeoverWithCleanStart0_ResumesTheOlderConnectionsSession`. Library coverage
  100% line and branch; fast tests green in all 78 test projects.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. A CONNECT for a connected MQTT 5 identifier sends the older connection DISCONNECT 0x8E, settles its Will, and resumes or discards its session by Clean Start
