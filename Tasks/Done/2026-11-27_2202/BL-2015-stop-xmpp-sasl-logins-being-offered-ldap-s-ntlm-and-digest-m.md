---
id: BL-2015
title: Stop XMPP SASL logins being offered LDAP's NTLM and DIGEST-MD5 security layers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Documentation/Planning/Decisions/ADR-0125-sasl-scram-sha-1-and-scram-sha-256-in-surl-authentication.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2015 — Stop XMPP SASL logins being offered LDAP's NTLM and DIGEST-MD5 security layers

## Goal

A server can start a SASL exchange whose success carries additional data without the exchange
offering or granting a security layer.

## Context

Found while working BL-1994 (2026-11-27), read from source and not yet reproduced by a test.
`SaslExchangeStart.CanCarrySecurityLayer` stands for two things. `XmppSaslLogin.StartAsync`
(`Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary`) passes `true` so that SCRAM's
server-final message returns as `SaslLoginStep.AdditionalSuccessData`. With `--auth digest-md5` or
`ntlm` the same flag makes `DigestMd5SaslExchange` offer `qop="auth,auth-int,auth-conf"` and
`NtlmSaslExchange` grant signing and sealing, refuse unchecked logins and unwrap SPNEGO. XMPP
ignores `SaslLoginStep.SecurityLayer`, so a client that negotiates a layer gets plain stanzas.

Add a separate flag to `SaslExchangeStart` (default `false`) for "the success can carry data";
`CanCarrySecurityLayer` keeps implying it, so LDAP is unchanged. `ScramSaslExchange` reads the new
meaning. BL-2016 then changes XMPP. See ADR-0072 decision 4, ADR-0114 decision 3 and ADR-0125.

## Acceptance criteria

- [x] `SaslExchangeStart` has a flag, `false` by default, that asks for additional success data without a security layer, documented as such.
- [x] A test starts `SCRAM-SHA-256` with only that flag and sees the server-final message in `AdditionalSuccessData`.
- [x] A test starts `DIGEST-MD5` with only that flag and sees a challenge offering `qop="auth"` alone and no `SecurityLayer`.
- [x] A test starts `NTLM` with only that flag and sees a challenge granting neither sign nor seal and no `SecurityLayer`.
- [x] The existing LDAP SASL tests pass unchanged, and both libraries stay at 100% line and branch coverage.

## Notes

- The flag is `SaslExchangeStart.CanCarryAdditionalSuccessData`, added last so every existing caller is unchanged. `CanCarrySecurityLayer` implies it (`SaslExchangeContext.SuccessCarriesData`).
- Decision (ADR-0125, amendment of 2026-11-27): `DIGEST-MD5` reads the flag as `SCRAM` does, sending `rspauth` with the success, still `qop="auth"` and no layer. `NTLM` and `GSS-SPNEGO` do not read it.
- `touches` gained ADR-0125 and the decision index for that amendment; no task in `Doing` names `Documentation`.
- Tests: `SaslSuccessDataWithoutSecurityLayerTests`, plus cases in `ScramSaslMechanismTests` and `MailAuthenticationContractTests`. Fast tests green; `Measure-CodeQuality.ps1` reports both libraries at 100% line and branch, 0 failing members. No LDAP test file changed.
- Feature stages: plan, tests and code were done in this session (about 20 lines of production code); review was `code-reviewer` (no must-fix; its test gaps fixed). Conformance skipped: no server sets the new flag yet, so no byte Surl sends changes. XMPP moves to it in BL-2016.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A server can ask for SASL success data with CanCarryAdditionalSuccessData without being offered or granted a security layer
