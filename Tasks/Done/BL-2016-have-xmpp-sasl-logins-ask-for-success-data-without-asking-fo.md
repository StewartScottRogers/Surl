---
id: BL-2016
title: Have XMPP SASL logins ask for success data without asking for a security layer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2015]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2016 — Have XMPP SASL logins ask for success data without asking for a security layer

## Goal

The XMPP server's SASL logins offer and grant no security layer, and SCRAM's server-final message
still arrives in `<success/>`.

## Context

Found while working BL-1994 (2026-11-27). `XmppSaslLogin.StartAsync` passes
`CanCarrySecurityLayer: true` to `SaslExchangeStart`, though the server ignores
`SaslLoginStep.SecurityLayer`. BL-2015 adds the flag that asks only for additional success data;
this task passes that one instead, and corrects the sentence about it in the library's `CLAUDE.md`
("SASL and resource binding"). RFC 6120 section 6.3.10; ADR-0114 decision 3.

## Acceptance criteria

- [x] A test in the XMPP `.UnitTests` project asserts the `SaslExchangeStart` has `CanCarrySecurityLayer` `false` and BL-2015's flag `true`.
- [x] The existing SCRAM success tests pass, with the server-final message in `<success/>`.
- [x] The library's `CLAUDE.md` names the new flag and no longer says the exchange is started with `CanCarrySecurityLayer: true`.
- [x] The library stays at 100% line and branch coverage.

## Notes

- Delivered in the lane session without delegating the `/feature` stages: the change is one argument,
  one assertion pair and one `CLAUDE.md` paragraph, so the agent hand-offs would cost more than they
  check. No new ADR: ADR-0125's BL-2015 amendment already decides it.
- `XmppSaslLogin` now passes `CanCarryAdditionalSuccessData: true` and leaves `CanCarrySecurityLayer`
  at `false`. XMPP offers only SCRAM, `PLAIN` and `ANONYMOUS` (ADR-0114 decision 3), so no answer a
  client sees changes; no conformance run (upstream curl does not speak XMPP).
- Tests first: the two data rows of the SCRAM success test failed on `Assert.IsFalse(start.CanCarrySecurityLayer)`
  before the change. After it: build 0 warnings, format clean, 83 test assemblies, 30553 passed,
  0 failed, 3 skipped (fast tests). `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Xmpp.UnitLibrary`:
  line 100, branch 100, 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. XMPP SASL logins ask for success data only; no security layer is offered or granted, and SCRAM's server-final still arrives in <success/>
