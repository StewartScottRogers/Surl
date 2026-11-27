---
id: BL-1903
title: Give the composed MQTT 5 broker its SASL policy so surl serves enhanced authentication
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1903 — Give the composed MQTT 5 broker its SASL policy so surl serves enhanced authentication

## Goal

A `CONNECT` with an Authentication Method that `--auth` accepts logs in over `mqtt5`, `mqtt5s` and `mqtt5+ws` as `surl` composes them, or an ADR says why not.

## Context

- Found by BL-1891, by reading the code.
- `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` builds `Mqtt5Broker` without its `saslAuthenticationPolicy` argument, so it is `null` and every Authentication Method is answered `0x8C`, though `Mqtt5EnhancedAuthentication` serves it. Read the MQTT 5 ADR (BL-494) first for whether this is intended.
- The `--auth` texts in `Surl.Cli.UnitLibrary` name the servers whose SASL mechanisms `--auth` sets; MQTT 5.0 joins them once wired, with `mqtt5` in the option's help categories.

## Acceptance criteria

- [x] A named test in `Surl.Composition.UnitTests` completes an enhanced authentication against the composed `mqtt5` server, or an ADR records why `surl` serves none and the `mqtt5` AI help paragraph says so.
- [x] If wired, the `--auth` option page, manual paragraph and AI help paragraph name the MQTT 5.0 server and `surl --help mqtt5` lists `--auth`.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Wired. ADR-0093 decision 2 intends enhanced authentication; the composition only lacked the argument.
  `ExtendedProtocolServers` now passes `AuthenticationPolicyFor("mqtt5")` as the broker's SASL policy too.
- Named test: `Mqtt5EnhancedAuthenticationCompositionTests.ComposeAsync_Mqtt5ConnectWithAScramMethodAuthAccepts_CompletesEnhancedAuthentication`
  (SCRAM-SHA-256 and SCRAM-SHA-1; both failed before the fix). `mqtt5s` and `mqtt5+ws` use the same broker.
- Default kept: SCRAM stays out of the `mqtt5` accepted set without `--auth`, so such a start answers `0x8C` as
  before. ADR-0179 adds the default for `kafka` and `xmpp` only. Recorded in ADR-0093 decision 2 (amendment).
- Added `Documentation/Planning/Decisions` to `touches` for that amendment; no task in Doing names it.
- No upstream curl measurement: `mqtt5` is an extended scheme (ADR-0083); test bytes are from MQTT 5.0 and RFC 5802.
- Verify: build 0 warnings, format clean, fast tests 30279 passed, 3 skipped, 0 failed.
- Follow-up filed: BL-1911 (an account restricted to `mqtt5+ws`).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl's composed MQTT 5 broker serves SCRAM enhanced authentication when --auth names it, and the --auth help names it
