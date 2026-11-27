---
id: BL-1963
title: Correct the composer and not-yet-built claims in the Surl.Authentication comments and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1963 — Correct the composer and not-yet-built claims in the Surl.Authentication comments and notes

## Goal

The comments and `CLAUDE.md` of `Surl.Authentication` name `Surl.Composition` as the project that composes the
authentication policy, and the notes no longer describe built behaviour as missing or replaced behaviour as current.

## Context

Found while working BL-1949 (2026-11-27), read from source. XML doc comments and project-notes text only; no code,
string literal or behaviour change. All paths are in `Foundation/Surl.Authentication.UnitLibrary`; line numbers are as
of that date and approximate. The library does not reference `Surl.Composition`, so name its types in `<c>`, not
`<see cref>`.

What the code does (`Application/Surl.Composition.UnitLibrary`): `AuthenticationComposition.Compose` reads the
`--user-file`, `--authorized-keys` and `--keytab` files through the `SurlServerCompositionSeams.ReadStartFile` seam
(the disk when none is given), calls `UserFileParser.Parse` and `AuthorizedKeysParser.Parse`, builds
`AuthenticationSettings` (with `KerberosAcceptor`), and `ComposePolicy` hands the acceptor to
`NegotiateAuthenticationMethod`. It also builds each refusal message, `(2) User file <path>, <Describe()>` and
`(2) Authorized keys <path>, <Describe()>`, which the host then writes after `surl: `.

Comments and notes that say `Surl.Console` does this:

- `AuthenticationSettings.cs`, about line 7.
- `AuthorizedKeysParser.cs`, about line 13.
- `UserFileParser.cs`, about line 10.
- `UserFileLineFailure.cs`, about line 16, and `AuthorizedKeysLineFailure.cs`, about line 20: "`Surl.Console` writes".
- `AuthenticationPolicy.cs`, the `Settings` property, about line 655.
- `CLAUDE.md`, about lines 22-26: the acceptor `Surl.Console` builds, hands to `NegotiateAuthenticationMethod`, and
  `Surl.Console`'s `AuthenticationComposition`.

Other stale sentences in the same `CLAUDE.md`:

- About lines 13-14: "Not here yet: `Proxy-Authenticate`, the logins of servers not yet built (FTP, SMB)". SMB logins
  are checked by `AuthenticationPolicy.CheckSmbNtlmV1LoginAsync`, the `ftp` server is composed with the policy, and
  the HTTP proxy judges `Proxy-Authorization` through a policy that
  `AuthenticationComposition.ComposeProxyPolicy` builds (`HttpProxyExchange.Login.cs` in
  `Surl.Protocol.HttpProxy`).
- About lines 125-127: the rest of `NtlmV1Calculation` "waits for the SMB login check (BL-295)". BL-295 is done and
  `CheckSmbNtlmV1LoginAsync` calls `NtlmV1Calculation.ComputeResponse`.
- About lines 529-530: a proxy given no proxy account "has `AnonymousAuthenticationPolicy`, which accepts any ticket".
  `ComposeProxyPolicy` gives it `Surl.Composition`'s `NoAccountProxyAuthenticationPolicy`, which asks for no login but
  judges a Kerberos login through a policy that publishes it and applies `refuse-login`.

No store, event or fault changes: documentation only.

## Acceptance criteria

- [ ] No `.cs` comment or `CLAUDE.md` line in the library says `Surl.Console` composes, builds, reads or hands anything.
- [ ] The comments of `UserFileParser`, `AuthorizedKeysParser`, `AuthenticationSettings`, both line-failure records and `AuthenticationPolicy.Settings` name `Surl.Composition` for the step it performs.
- [ ] `CLAUDE.md` no longer says the FTP or SMB logins or the SMB login check are still to come, and says where `Proxy-Authorization` is judged.
- [ ] `CLAUDE.md` says a proxy given no proxy account has `NoAccountProxyAuthenticationPolicy` and what it does with a Kerberos login.
- [ ] The diff changes only `CLAUDE.md` and `///` comment lines in the library.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
