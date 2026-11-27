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
completed:
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

- [ ] The summaries of all four interfaces name `Surl.Composition` as the project that passes the policy to the server or servers.
- [ ] None of the four files says `Surl.Console`.
- [ ] The diff changes only `///` comment lines in those four files.
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
