---
id: BL-976
title: Add --mqtt5-disconnect-after-retained and pass it to the MQTT 5 broker
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-975]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
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

- [ ] A named `Surl.Cli.UnitTests` test reads the flag and its negation.
- [ ] A named `Surl.Composition.UnitTests` test shows the flag reaching the broker, and off when absent.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, and `--aihelp mqtt5` lists the flag.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-06: Created.
