---
id: BL-1962
title: Name Surl.Composition as the composer in the authentication policy contract comments
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary/IAuthenticationPolicy.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/IMailAuthenticationPolicy.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/ISmbAuthenticationPolicy.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/ISshAuthenticationPolicy.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1962 — Name Surl.Composition as the composer in the authentication policy contract comments

## Goal

The XML doc comments of the four authentication policy contracts name `Surl.Composition`, not `Surl.Console`, as the
project that passes the policy to the servers.

## Context

Found while working BL-1949 (2026-11-27), read from source. XML doc comments only; no code or behaviour change. Line
numbers are as of that date and approximate.

In `Foundation/Surl.Protocol.Abstractions.UnitLibrary`:

- `IAuthenticationPolicy.cs`, about line 5: "`Surl.Console` passes that implementation to each server".
- `IMailAuthenticationPolicy.cs`, about line 8: "`Surl.Console` passes the same object to the mail servers as both".
- `ISmbAuthenticationPolicy.cs`, about line 5: "`Surl.Console` passes the same object to the SMB server".
- `ISshAuthenticationPolicy.cs`, about line 5: "`Surl.Console` passes the same object to the SSH server".

What the code does (`Application/Surl.Composition.UnitLibrary`): `ProtocolServerRegistrations` (and
`ExtendedProtocolServers`) give each server `ComposedProtocolServices.AuthenticationPolicyFor(scheme)`, an
`AuthenticationPolicy` that `SchemeAuthenticationPolicies.For` composes once per scheme over the accounts allowed on
that scheme (ADR-0102 decision 4.3). The `imap`, `pop3` and `smtp` servers get that one object as both
`IAuthenticationPolicy` and `IMailAuthenticationPolicy`; the SMB server takes it as `ISmbAuthenticationPolicy`; the
`scp` and `sftp` servers take it as `ISshAuthenticationPolicy`. The Abstractions library references no composition
project, so name its types in `<c>`, not `<see cref>`.

No store, event or fault is involved: comments only.

## Acceptance criteria

- [x] The summaries of all four interfaces name `Surl.Composition` as the project that passes the policy to the server or servers.
- [x] None of the four files says `Surl.Console`.
- [x] The diff changes only `///` comment lines in those four files.
- [x] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

- `align-and-document` rewrote the four interface summaries. Beyond the composer's name, `IAuthenticationPolicy` no
  longer says "each server": it says each server that takes a policy gets one over its scheme's accounts (ADR-0102
  decision 4.3), and that the HTTP and SOCKS proxies get one over the proxy accounts or
  `NoAccountProxyAuthenticationPolicy`. `ISshAuthenticationPolicy` says `scp` and `sftp` each get their own scheme's
  policy, not one shared object.
- Verified 2026-11-27: `dotnet build -warnaserror` of the solution 0 warnings, `dotnet format --verify-no-changes`
  clean, fast tests 30378 passed, 0 failed, 3 skipped.
- Stale member comments in `IAuthenticationPolicy.cs`, outside this task's criteria, are filed as BL-1974. The
  `Surl.Authentication` notes that name `Surl.Console` are already BL-1963.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The four authentication policy contract summaries name Surl.Composition as the composer
