---
id: BL-1903
title: Give the composed MQTT 5 broker its SASL policy so surl serves enhanced authentication
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1903 — Give the composed MQTT 5 broker its SASL policy so surl serves enhanced authentication

## Goal

A `CONNECT` with an Authentication Method that `--auth` accepts logs in over `mqtt5`, `mqtt5s` and `mqtt5+ws` as `surl` composes them, or an ADR says why not.

## Context

- Found by BL-1891, by reading the code.
- `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` builds `Mqtt5Broker` without its `saslAuthenticationPolicy` argument, so it is `null` and every Authentication Method is answered `0x8C`, though `Mqtt5EnhancedAuthentication` serves it. Read the MQTT 5 ADR (BL-494) first for whether this is intended.
- The `--auth` texts in `Surl.Cli.UnitLibrary` name the servers whose SASL mechanisms `--auth` sets; MQTT 5.0 joins them once wired, with `mqtt5` in the option's help categories.

## Acceptance criteria

- [ ] A named test in `Surl.Composition.UnitTests` completes an enhanced authentication against the composed `mqtt5` server, or an ADR records why `surl` serves none and the `mqtt5` AI help paragraph says so.
- [ ] If wired, the `--auth` option page, manual paragraph and AI help paragraph name the MQTT 5.0 server and `surl --help mqtt5` lists `--auth`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
