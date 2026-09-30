---
id: BL-308
title: Answer LDAP simple binds, searches, unbind and abandon in Surl.Protocol.Ldap
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-306]
touches: [Surl.Protocol.Ldap.UnitLibrary, Surl.Protocol.Ldap.UnitTests]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-308 — Answer LDAP simple binds, searches, unbind and abandon in Surl.Protocol.Ldap

## Goal

`LdapProtocolServer` in `Surl.Protocol.Ldap` implements `IConnectionProtocolServer` for `ldap` and
answers the pinned Windows reference build's simple bind (through `CheckPasswordLoginAsync`), its
anonymous and version 2 retry binds, its search, unbind and abandon, and every unknown or malformed
operation, as BL-284's ADR decides, replaying request bytes recorded from that build.

## Context

- Decisions: BL-284's ADR (the bind DN to account mapping, the result code for each refused bind,
  a simple bind on `ldap://` refused unchecked without `--allow-plaintext-auth` per ADR-0032
  criterion 3, what `--allow-anonymous` opens, what each identity may search, the answer to an LDAP
  version 2 bind, `protocolError` and the Notice of Disconnection, the limits - `--max-message`, idle
  timeout, maximum duration - and what each sends, the verbose and trace notes).
- Code: BL-289's codec, BL-306's directory, `IAuthenticationPolicy.CheckPasswordLoginAsync` and
  `PasswordLogin` in `Surl.Protocol.Abstractions` (ADR-0032 section 6, ADR-0038's login note). The
  server never references `Surl.Authentication` (ADR-0002 decision 3).
- Pattern: `Surl.Protocol.Mqtt.UnitLibrary/MqttProtocolServer.cs` (a binary-framed server with a
  password login) and its tests; ADR-0059 tells a limit from shutdown.
- `WinLDAP`'s NTLM, Negotiate and Digest binds (curl's `--ntlm`, `--negotiate`, `--digest`) and a
  bind with no `-u` are SASL binds: this task answers them with the ADR's refusal until BL-309 adds
  SASL.
- Fixtures: BL-284's cases recorded again with `Record-CurlExchange.ps1` against the Windows
  reference build, committed as test data (upstream bytes only, ADR-0003).

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Ldap.UnitTests` replay the recorded bind and search of each ADR case
      over `InMemoryConnection` and answer them with the ADR's bytes: a base, one-level and subtree
      search, a filtered search, an empty result, an accepted simple bind, a wrong password, no
      accounts, a plain-text bind refused unchecked and accepted with `--allow-plaintext-auth`, an
      anonymous search with and without `--allow-anonymous`, the version 2 retry, unbind, abandon, an
      unknown operation, a malformed message, a message past `--max-message`, the idle timeout and the
      maximum duration; no test opens a socket.
- [ ] No log note carries a password.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
