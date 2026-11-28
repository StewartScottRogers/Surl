---
id: BL-2011
title: Stop XMPP SASL logins being offered LDAP's NTLM and DIGEST-MD5 security layers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2011 — Stop XMPP SASL logins being offered LDAP's NTLM and DIGEST-MD5 security layers

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
meaning. BL-2012 then changes XMPP. See ADR-0072 decision 4, ADR-0114 decision 3 and ADR-0125.

## Acceptance criteria

- [ ] `SaslExchangeStart` has a flag, `false` by default, that asks for additional success data without a security layer, documented as such.
- [ ] A test starts `SCRAM-SHA-256` with only that flag and sees the server-final message in `AdditionalSuccessData`.
- [ ] A test starts `DIGEST-MD5` with only that flag and sees a challenge offering `qop="auth"` alone and no `SecurityLayer`.
- [ ] A test starts `NTLM` with only that flag and sees a challenge granting neither sign nor seal and no `SecurityLayer`.
- [ ] The existing LDAP SASL tests pass unchanged, and both libraries stay at 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
