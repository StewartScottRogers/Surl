---
id: BL-2056
title: Correct the AllowAnonymous, AuthenticationPolicy, SASL exchange and AccountBook descriptions in Surl.Authentication
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-2056 — Correct the AllowAnonymous, AuthenticationPolicy, SASL exchange and AccountBook descriptions in Surl.Authentication

## Goal

The `AllowAnonymous`, `AuthenticationPolicy`, SASL exchange, `SaslMechanism`, `AccountBook` and
`NamedAccountCredentials` descriptions in `Surl.Authentication` are true of the code.

## Context

Found while working BL-2014 (2026-11-27), read from source. Documentation only; every file is in
`Foundation/Surl.Authentication.UnitLibrary`. `ExternalSaslExchange`'s summary is BL-2050, not this task.

- `AuthenticationSettings.cs`, `AllowAnonymous`: names `--allow-anonymous` only, but
  `AuthenticationComposition.ComposeProxyPolicy` sets it for the Kerberos policy of a proxy given no
  proxy account, without the option.
- `AuthenticationPolicy.cs`, class summary: "of the `AuthenticationMethod`s only the accepted ones are
  offered and checked" is not true of SOCKS5 GSS-API (`JudgeKerberosLoginAsync`), which `--auth` and
  `--proxy-auth` do not change (ADR-0165); the SSH sentence leaves out the LDAP account source.
- `ScramSaslExchange.cs`: names only the mail servers as sending no data with success; AMQP, Kafka and
  MQTT 5.0 start exchanges the same way.
- `NtlmSaslExchange.cs`: "For the mail servers" applies to every exchange without a security layer.
- `SaslMechanism.cs`: the type summary limits mechanisms to the mail servers, and `GssSpnego`'s reason
  for its separate listing names the mail servers alone, though only `ldap` and `ldaps` offer it.
- `AccountBook.cs` and `NamedAccountCredentials.cs`: "challenge-response mail logins"; LDAP and AMQP run
  CRAM-MD5 and DIGEST-MD5 too.

## Acceptance criteria

- [ ] The `AllowAnonymous` description and the `AuthenticationPolicy` class summary are true of the no-account proxy's Kerberos policy, SOCKS5 GSS-API and SSH password logins.
- [ ] No `ScramSaslExchange`, `NtlmSaslExchange`, `SaslMechanism`, `AccountBook` or `NamedAccountCredentials` description limits to the mail servers what another server runs.
- [ ] The diff changes only `///` lines and, if needed, the library's `CLAUDE.md`.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
