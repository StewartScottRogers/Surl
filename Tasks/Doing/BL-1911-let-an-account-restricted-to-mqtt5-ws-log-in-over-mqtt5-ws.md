---
id: BL-1911
title: Let an account restricted to mqtt5+ws log in over mqtt5+ws
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-27
completed:
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

- [ ] A named test in `Surl.Composition.UnitTests` shows what a `CONNECT` over `mqtt5+ws` gets for an account
      restricted to `mqtt5+ws`, and what the same account gets over `mqtt5`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
