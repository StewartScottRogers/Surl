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
completed: 2026-09-30
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

- [x] Tests in `Surl.Protocol.Ldap.UnitTests` replay the recorded bind and search of each ADR case
      over `InMemoryConnection` and answer them with the ADR's bytes: a base, one-level and subtree
      search, a filtered search, an empty result, an accepted simple bind, a wrong password, no
      accounts, a plain-text bind refused unchecked and accepted with `--allow-plaintext-auth`, an
      anonymous search with and without `--allow-anonymous`, the version 2 retry, unbind, abandon, an
      unknown operation, a malformed message, a message past `--max-message`, the idle timeout and the
      maximum duration; no test opens a socket.
- [x] No log note carries a password.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

- **Built:** `LdapProtocolServer` (public, scheme `ldap`; `ldaps` joins with BL-309) with a
  public constructor over an empty directory (in-memory mode) and an internal one over an
  `LdapDirectory`, since BL-307/BL-310 will load one; `LdapSession` per connection;
  `LdapBindJudge`, `LdapBindNames`, `LdapDiagnostics`, `LdapLogText`. The codec gained
  `CompareRequest` decoding, `EncodeResultResponse`, and `LdapFrameReadResult.AnnouncedBytes`
  so the `--max-message` Notice can say `a message of <n> bytes`. `LdapDirectory.Compare`
  answers ADR-0072 decision 3's compare.
- **Fixtures:** seven cases recorded again with `Record-CurlExchange.ps1 -Ldap` from the pinned
  Windows build (`Surl.Protocol.Ldap.UnitTests/Fixtures/README.md`). `WinLDAP` never sends
  abandon (a search left unanswered 40 s drew none), and no curl sends an unknown operation, a
  malformed message, compare or writes: those are built by hand in
  `LdapProtocolServerOperationTests`. The anonymous search cases replay the recorded search
  message without its bind, since the Windows build never binds anonymously (ADR-0072).
- **Decisions (sensible defaults, recorded for BL-331 to put in ADR-0072, whose folder BL-286
  held):** the idle timeout and maximum duration share one Notice, `unavailable`
  `idle timeout or maximum duration`, because ADR-0059 decision 5 gives a server no reason;
  an extended operation is `protocolError` `unsupported extended operation`; a critical
  control `unavailableCriticalExtension` `critical control not supported`; Sicily and SASL
  binds `authMethodNotSupported` `only simple binds are answered`; a version other than 2 or
  3 `protocolError`; a bind DN no account maps from goes to the policy whole, so it fails 49
  after the same delay as a wrong password; a compare the rule cannot decide is
  `compareFalse`, one of a DN that is not RFC 4514 `invalidDNSyntax`;
  `LdapProtocolServer.MaxFilterDepth` is 64. Whether an unbound connection may read is asked
  of the policy as a login with no credentials: `AcceptedUnchecked` means `--allow-anonymous`.
  Every Notice of Disconnection is written within the one-second limit-reply deadline linked
  to shutdown alone (ADR-0059 decision 3).
- **Bug the tests caught:** `cond ? null : new ReadOnlyMemory<byte>(bytes)` is never null -
  `null` converts through `ReadOnlyMemory`'s implicit conversion from `byte[]` - so an
  anonymous bind reached the policy as a password login. Fixed with an `if`.
- **Measured:** `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ldap.UnitLibrary`: 100% line,
  100% branch, 260 members, 0 failing, worst CRAP 10. 386 tests in
  `Surl.Protocol.Ldap.UnitTests`. Conformance against upstream curl waits for registration
  (BL-310) and is BL-311's.
- **Filed:** BL-331 (docs, Low): amend ADR-0072 with the decisions above.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. LdapProtocolServer answers ldap simple binds, searches, compare, unbind, abandon, refused writes and the Notice of Disconnection, replaying recorded WinLDAP bytes
