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
completed: 2026-11-28
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

- [x] The `AllowAnonymous` description and the `AuthenticationPolicy` class summary are true of the no-account proxy's Kerberos policy, SOCKS5 GSS-API and SSH password logins.
- [x] No `ScramSaslExchange`, `NtlmSaslExchange`, `SaslMechanism`, `AccountBook` or `NamedAccountCredentials` description limits to the mail servers what another server runs.
- [x] The diff changes only `///` lines and, if needed, the library's `CLAUDE.md`.
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

- Each statement was checked against the callers: `AuthenticationComposition.ComposeProxyPolicy`,
  `JudgeKerberosLoginAsync` and the `StartSaslExchange` callers in the protocol servers.
- Also corrected, since the goal covers `AuthenticationPolicy` descriptions: its constructors'
  `timeProvider` and `nonceSource` parameters named only some of their uses (`nonceSource` also
  feeds SCRAM server nonces).
- Two lines of the library's `CLAUDE.md` said "mail" where the behaviour is that of every exchange
  without a security layer; both now say so.
- No ADR: no design or behaviour decision. No follow-up task filed.
- 2026-11-28 verification: `dotnet build -warnaserror` 0 warnings, `dotnet format
  --verify-no-changes` clean, fast tests 0 failed (`Surl.Authentication.UnitTests` 1624 passed,
  rerun after the last comment edit).

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The AllowAnonymous, AuthenticationPolicy, SASL exchange, SaslMechanism and account credential descriptions in Surl.Authentication match the code
