---
id: BL-1224
title: Hand each connection an engine ITlsLoginJudge that publishes LoginJudgedEvent and applies refuse-login (ADR-0165)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1214, BL-1221, BL-1223]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1224 — Hand each connection an engine ITlsLoginJudge that publishes LoginJudgedEvent and applies refuse-login (ADR-0165)

## Goal

The engine gives each connection an ITlsLoginJudge that publishes LoginJudgedEvent (mechanism tls-srp) and applies refuse-login.

## Context

ADR-0165 (Documentation/Planning/Decisions) decides this. No wire byte changes; existing upstream curl 8.21.0 fixtures stay green.

## Acceptance criteria

- [x] Per ADR-0165 decisions 1, 4 and 5: FaultPoint at FaultStage.Login with exchange, listener, scheme, account; FaultAppliedEvent then LoginJudgedEvent (IsRefusedByFault); non-RefuseLogin rules ignored; AIHelp/docs facts for fault stage updated if they list TLS-SRP.
- [x] dotnet build is clean and the fast tests pass; the project stays at 100% coverage.

## Notes

ExchangeTlsLoginJudge is handed to each raw connection right after OpenExchange, so implicit TLS and STARTTLS share it. Only RefuseLogin rules can sit at Login in a plan; a non-RefuseLogin rule is reachable only through an override and is ignored. Core CLAUDE.md updated; no AiHelp text lists TLS-SRP login faults.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Engine judge published and tested
