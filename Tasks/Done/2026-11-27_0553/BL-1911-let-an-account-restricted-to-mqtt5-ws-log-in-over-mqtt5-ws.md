---
id: BL-1911
title: Let an account restricted to mqtt5+ws log in over mqtt5+ws
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Documentation/Planning/Decisions/ADR-0093-how-the-mqtt-5-broker-answers-mqtt5-mqtt5s-and-mqtt5-ws.md, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1911 — Let an account restricted to mqtt5+ws log in over mqtt5+ws

## Goal

An account restricted by `protocols=mqtt5+ws` logs in over `mqtt5+ws` and stays refused on `mqtt5` and `mqtt5s`, or `--aihelp mqtt5` says such an account cannot log in.

## Context

- Found by BL-1903 on 2026-11-27 by reading the code; not reproduced.
- `ExtendedProtocolServers.Registrations` builds the one `Mqtt5Broker` with `AuthenticationPolicyFor("mqtt5")`, and
  `mqtt5+ws` wraps that broker, so its logins are judged by the `mqtt5` view of the accounts.
- `Account.MayLogInOn` admits a named scheme or its TLS twin, so `protocols=mqtt5+ws` does not admit `mqtt5`.
- ADR-0102 decision 4.3 and its amendment say which view each registration takes; ADR-0093 decision 7 says one
  broker serves the three schemes, so sessions and the bus stay shared.

## Acceptance criteria

- [x] A named test in `Surl.Composition.UnitTests` shows what a `CONNECT` over `mqtt5+ws` gets for an account
      restricted to `mqtt5+ws`, and what the same account gets over `mqtt5`.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Reproduced first: `Mqtt5WebSocketAccountSchemeCompositionTests` failed 2 of its 8 rows before the fix
  (`protocols=mqtt5+ws` got `0x86` over `mqtt5+ws`; `protocols=mqtt5` got `0x00` there). After it, `mqtt5+ws` gets
  `0x00` and `mqtt5` and `mqtt5s` get `0x86`.
- Fix: two `Mqtt5Broker`s over one `ComposedProtocolServices.Mqtt5BrokerState`; the one behind `mqtt5+ws` judges
  logins by `AuthenticationPolicyFor("mqtt5+ws")`. Sessions, takeover, subscriptions and Wills stay shared, and only
  the `mqtt5` broker is a registered server, so the sessions store resets the state once.
- Decision (Claude under Stewart's delegation; ADR-0102 decision 4.3 amendment, ADR-0093 decision 0 amendment):
  `protocols=mqtt5` no longer admits `mqtt5+ws`; `mqtt5,mqtt5+ws` admits all three. `mqtt5+ws` is its own scheme,
  not a TLS twin, as `ws` is not one of `http`, and the narrower reading is the secure one.
- `touches` widened: the two ADRs record the decision, and `Mqtt5BrokerState`'s doc comment and that library's
  `CLAUDE.md` said the state belongs to one broker. No task in `Doing` names any of them.
- `--aihelp` needs no change: it states no rule about which MQTT 5 view `mqtt5+ws` takes.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. An account restricted to mqtt5+ws logs in over mqtt5+ws and is refused on mqtt5 and mqtt5s; mqtt5 alone no longer admits mqtt5+ws (ADR-0102 4.3 amendment)
