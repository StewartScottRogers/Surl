---
id: BL-976
title: Add --mqtt5-disconnect-after-retained and pass it to the MQTT 5 broker
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-975]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Mcp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-976 — Add --mqtt5-disconnect-after-retained and pass it to the MQTT 5 broker

## Goal

`surl --mqtt5-disconnect-after-retained mqtt5://127.0.0.1:0` makes the broker send DISCONNECT `0x00` and close
after a SUBACK and the retained messages it releases, as ADR-0093 decision 9 says; without the flag it does not.

## Context

- Second of the two tasks BL-501 was split into (see BL-975).
- Decision: ADR-0093 decision 9 - the negatable flag `--mqtt5-disconnect-after-retained` (category `mqtt5`, default
  off), so upstream curl's one-shot `mqtt` subscribe pattern ends against the MQTT 5 broker. It needs its
  `OptionArgumentType`, help, `ManualText` line and `--aihelp` facts like any option (ADR-0034, ADR-0046).
- Composition: where BL-975 registers the broker in `Application/Surl.Composition.UnitLibrary`.
- Stand-in: no store change.

## Acceptance criteria

- [x] A named `Surl.Cli.UnitTests` test reads the flag and its negation.
- [x] A named `Surl.Composition.UnitTests` test shows the flag reaching the broker, and off when absent.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, and `--aihelp mqtt5` lists the flag.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Touches widened (no task in Doing named them): the MQTT 5 library and its tests, because BL-975 left the broker with no one-shot option to pass the flag to (`Mqtt5Broker` gained `disconnectsAfterRetained`, `DisconnectsAfterRetained`); `Surl.Mcp.UnitLibrary`, whose `CommandLinePathMembers` must classify the new `SurlCommandLine` member.
- Decided by Claude under Stewart's delegation: with the option on, the connection stops its sending loop, writes the retained messages itself in order (`Mqtt5OutboundDeliveries.SendAsync`), then DISCONNECT `0x00`, ending `ClosedNormally` so the Will is discarded (ADR-0093 decision 9). A retained QoS 1/2 message past the client's Receive Maximum would wait for an acknowledgement nobody reads; accepted for a one-shot reader.
- Help text of the flag: "End a subscribe after retained messages" (the 79-column rule); manual section MQTT 5 OPTIONS; `mqtt5` aihelp paragraph added.

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. --mqtt5-disconnect-after-retained is read, composed and makes the MQTT 5 broker send DISCONNECT 0x00 after SUBACK and retained messages
