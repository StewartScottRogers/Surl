---
id: BL-1225
title: Implement AuthenticationPolicy.JudgeKerberosLoginAsync: publish and apply refuse-login (ADR-0165)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1214, BL-1221]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1225 — Implement AuthenticationPolicy.JudgeKerberosLoginAsync: publish and apply refuse-login (ADR-0165)

## Goal

AuthenticationPolicy overrides JudgeKerberosLoginAsync: publishes LoginJudgedEvent (mechanism gssapi) and applies refuse-login for the accepted principal.

## Context

ADR-0165 (Documentation/Planning/Decisions) decides this. No wire byte changes; existing upstream curl 8.21.0 fixtures stay green.

## Acceptance criteria

- [x] ADR-0165 decisions 7 to 9: refused token publishes empty Account; accepted asks the fault plan at FaultStage.Login with the principal; refusal returns false after RefusalDelay on the injected TimeProvider; notes carry no secret.
- [x] dotnet build is clean and the fast tests pass; the project stays at 100% coverage.

## Notes

Accepted token with no readable principal is judged under account "" (fault rules with an empty Account still match). Refusal delay counted from judgement start on the injected TimeProvider.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. AuthenticationPolicy.JudgeKerberosLoginAsync publishes gssapi LoginJudgedEvent and applies refuse-login
