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
completed:
---
# BL-1224 — Hand each connection an engine ITlsLoginJudge that publishes LoginJudgedEvent and applies refuse-login (ADR-0165)

## Goal

The engine gives each connection an ITlsLoginJudge that publishes LoginJudgedEvent (mechanism tls-srp) and applies refuse-login.

## Context

ADR-0165 (Documentation/Planning/Decisions) decides this. No wire byte changes; existing upstream curl 8.21.0 fixtures stay green.

## Acceptance criteria

- [ ] Per ADR-0165 decisions 1, 4 and 5: FaultPoint at FaultStage.Login with exchange, listener, scheme, account; FaultAppliedEvent then LoginJudgedEvent (IsRefusedByFault); non-RefuseLogin rules ignored; AIHelp/docs facts for fault stage updated if they list TLS-SRP.
- [ ] dotnet build is clean and the fast tests pass; the project stays at 100% coverage.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
