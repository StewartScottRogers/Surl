---
id: BL-1475
title: Decide whether a retained seed through Surl.Api or publish_message obeys --topic's retain filter as a client publish does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1475 — Decide whether a retained seed through Surl.Api or publish_message obeys --topic's retain filter as a client publish does

## Goal

A retained message seeded through `Surl.Api`'s `PublishAsync` or `surl-mcp`'s `publish_message` (retain true) and
one a client publishes follow one recorded rule about topics outside `--topic ...;retain`, and `--aihelp` and the
`Surl.Api` README say which.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1609-Surl-bug-report.md` item 4 (a check):
  a seed with retain true is kept under a topic outside `--topic`'s filter, while the same publish from curl is not
  retained.
- ADR-0176 (`Documentation/Planning/Decisions/ADR-0176-how-the-mqtt-3-1-1-server-treats-the-retain-flag-...md`)
  decided RETAIN 0 is not retained and `--topic ...;retain` keeps a topic's last message. Upstream curl cannot set
  RETAIN 1, so for a curl client `--topic ...;retain` is the only way to retain.
- Seeding is the test's own act of setting up state (stand-in rule: "Controllable"), so keeping a seed wherever the
  test puts it is defensible. Refusing it would make a test declare `--topic` just to seed. Either answer is fine
  once recorded.

## Acceptance criteria

- [x] An ADR-0176 amendment ("Decided by Claude under Stewart's delegation") says whether a seed obeys the retain
      filter, and why, for `Surl.Api`, `surl-mcp` and the control endpoint alike.
- [x] The code follows it; named tests in `Surl.Api.UnitTests` and `Surl.Mcp.UnitTests` pin a retained seed on a
      topic outside `--topic`'s filter (kept, or refused with the error the ADR names).
- [x] The `Surl.Api` README, the `publish_message` tool description and the `--aihelp mqtt` paragraph state the rule.
- [x] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- Decided: keep seeds, ADR-0176 amendment 1; no refusal error. Control endpoint shares the same SeedPublishAsync path.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. ADR-0176 amendment 1: seeds ignore the retain filter; tests and docs added
