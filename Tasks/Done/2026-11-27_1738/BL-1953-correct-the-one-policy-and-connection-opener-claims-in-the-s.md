---
id: BL-1953
title: Correct the one-policy and connection-opener claims in the Surl.Console FTP and LDAP test class summaries
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1942]
touches: [Application/Surl.Console.UnitTests/CommandLineRunnerFtpTests.cs, Application/Surl.Console.UnitTests/CommandLineRunnerLdapTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1953 — Correct the one-policy and connection-opener claims in the Surl.Console FTP and LDAP test class summaries

## Goal

The FTP and LDAP `CommandLineRunner` test class summaries name the policy that judges a login and the code that creates
the FTP data connection opener.

## Context

Found while working BL-1942 (2026-11-27), read from source. XML doc comments only; no code, test or behaviour change.

- `CommandLineRunnerFtpTests.cs` says "logins judged by the one policy" and `CommandLineRunnerLdapTests.cs` says "each
  bind judged by the one authentication policy". Each scheme has its own policy over the accounts allowed on it
  (ADR-0102 decision 4.3): `AuthenticationPolicyFor("ftp")` in `ProtocolServerRegistrations`, and
  `SchemePolicies.For("ldap")` in `SurlServerComposition.Start.cs`.
- `CommandLineRunnerFtpTests.cs` says the data connections are opened through "the opener the runner creates".
  `ComposedServing.CreateDataConnectionOpener` in `Surl.Composition.UnitLibrary` creates it, using
  `SurlServerCompositionSeams.CreateDataConnectionOpener` when given and a `SocketDataConnectionOpener` otherwise.

No store, event or fault is involved: comments only.

## Acceptance criteria

- [x] Both summaries say a login is judged by the scheme's own authentication policy.
- [x] The FTP summary no longer says the runner creates the data connection opener.
- [x] The diff to the two files changes only `///` comment lines.
- [x] `dotnet build Application/Surl.Console.UnitTests -warnaserror` is clean.

## Notes

- 2026-11-27: Edited the two class summaries directly in the lane session rather than through `align-and-document`:
  two comments, with both claims checked in `ProtocolServerRegistrations.cs`, `SurlServerComposition.Start.cs`,
  `ComposedServing.cs` and `AccountBook.ForScheme`. No decision, so no ADR.
- The FTP summary names both openers (the `CreateDataConnectionOpener` seam's, else `SocketDataConnectionOpener`);
  each summary names the scheme (`ftp`, `ldap`) whose account view the policy is built over, which also admits its
  TLS twin (`Account.SchemesAdmit`).
- Verified: `dotnet build` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 30377 passed, 0 failed,
  3 skipped. Integration tests not run: only comments changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The FTP and LDAP CommandLineRunner test summaries name the scheme's own authentication policy and Surl.Composition's data connection opener
