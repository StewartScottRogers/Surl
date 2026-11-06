---
id: BL-349
title: Abandon a SASL exchange in progress when a Sicily response bind is refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Ldap.UnitLibrary, Surl.Protocol.Ldap.UnitTests, Documentation/Planning/Decisions/ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md]
requirement: FR-052
created: 2026-10-01
completed: 2026-10-01
---
# BL-349 — Abandon a SASL exchange in progress when a Sicily response bind is refused

## Goal

A Sicily `[11]` `sicilyResponse` bind that arrives while a SASL (not Sicily) exchange is in
progress is refused `protocolError` (2) and abandons that SASL exchange, as every other
non-continuing bind does (RFC 4513 section 5.2.1.2).

## Context

- `Surl.Protocol.Ldap.UnitLibrary/LdapSaslBindJudge.cs`, `JudgeAsync(LdapSicilyAuthentication)`:
  the `_` arm refuses `sicilyResponse without sicilyNegotiate` without calling `Abandon()`, so a
  later SASL bind naming the same mechanism still continues the old exchange.
- ADR-0072 decision 4, "Which bind continues an exchange", records the defect and names this task
  (found by BL-341).

## Acceptance criteria

- [x] A test in `Surl.Protocol.Ldap.UnitTests` starts a SASL exchange, sends a Sicily `[11]`
      (answered `protocolError`, `sicilyResponse without sicilyNegotiate`), then a SASL bind
      naming the same mechanism, and shows that bind starts a new exchange rather than
      continuing the old one.
- [x] ADR-0072 decision 4 no longer says a refused `[11]` leaves a SASL exchange in progress.
- [x] `dotnet build` is clean and the fast tests are green, with `Surl.Protocol.Ldap.UnitLibrary`
      at 100% line and branch coverage.

## Notes

- Fix: LdapSaslBindJudge's refused-`[11]` arm now calls `RefuseSicilyResponseWithoutNegotiate`, which `Abandon()`s before refusing. Test: `ServeAsync_SaslBindInProgressThenARefusedSicilyResponse_AbandonsTheSaslExchange` (two starts, no responses). Ldap UnitLibrary 100% line and branch; 494 Ldap tests green.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A refused Sicily [11] now abandons a SASL exchange in progress (RFC 4513 5.2.1.2)
