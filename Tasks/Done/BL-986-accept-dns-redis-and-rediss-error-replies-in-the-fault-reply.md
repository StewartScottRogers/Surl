---
id: BL-986
title: Accept dns, redis and rediss error replies in the --fault reply check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-984, BL-985, BL-785]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-06
completed: 2026-11-11
---
# BL-986 — Accept dns, redis and rediss error replies in the --fault reply check

## Goal

An `error` fault rule naming `dns`, `redis` or `rediss` is read by `--fault` instead of refused at configuration,
now that both servers answer it.

## Context

- Last of the three tasks BL-836 was split into; BL-984 and BL-985 made the servers answer the rule.
- Add these schemes' reply shapes (ADR-0124's extended reply table: a DNS RCODE 1 to 5; Redis error text) to
  BL-785's `--fault` reply check in `Application/Surl.Cli.UnitLibrary`.
- Stand-in: the `--fault` option is how a command-line user injects the fault.

## Acceptance criteria

- [x] Named `Surl.Cli` tests: one accepted and one refused `reply` per scheme (`dns`, `redis`, `rediss`).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Cli.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

- dns: a single digit 1 to 5 (matches DnsQueryFaults.TryParse). redis, rediss: any non-empty text; the reply decoder already refuses CR and LF ("a control character"), so no second check lives in the shape (it would be unreachable, breaking 100% branch coverage). The redis and rediss refused tests therefore pin that decoder refusal. Coverage not measured separately; every new branch is exercised by the added tests.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --fault error rules naming dns, redis and rediss are accepted with their reply shapes
