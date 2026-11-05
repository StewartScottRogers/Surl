---
id: BL-881
title: Compose each server with the authentication policy for its scheme in Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-881 — Compose each server with the authentication policy for its scheme in Surl.Composition

## Goal

`Surl.Composition` composes every server that has a login with the `AuthenticationPolicy` for its own scheme,
from BL-647's `AuthenticationSettings.ForScheme`. An account restricted by `protocols=` is then refused on
every other scheme, exactly as no such account. A start with no restricted account composes exactly as before.

## Context

- Part of BL-653's split (timed out after 120 min; Stewart asked for the split 2026-11-05). Independent of
  BL-880, so the two can run side by side. BL-882 depends on both.
- Decision: ADR-0102 decision 4.3 (one view of the accounts per scheme; the plaintext scheme's view admits its
  TLS twin). BL-647 (Done) built the library side in `Foundation/Surl.Authentication.UnitLibrary`. Its Context
  says the composition wiring is this work.
- The pieces:
  - `SchemeAuthenticationPolicies`: `For(scheme)` returns the start's one policy itself when
    `ForScheme` returns the same settings, otherwise one composed policy per scheme, cached. This needs
    `AuthenticationComposition.ComposePolicy` to become `internal`.
  - `ComposeProtocolServers` takes an optional `Func<string, AuthenticationPolicy>`, and each server with a
    login gets `policyFor(<its scheme>)`.
  - The SSH server is composed twice, once for `scp` and once for `sftp`, each wrapped in
    `SingleSchemeServer`, so `protocols=sftp` can admit one and not the other.
  - `ExtendedProtocolServerServices` gains `AuthenticationPolicyForScheme` and `AuthenticationPolicyFor(scheme)`.
  - The HTTP/3 server is composed with `For("https")`, and the LDAP service state with `For("ldap")`.
  - `ComposedAuthentication` carries the `SchemeAuthenticationPolicies`.
- **Reuse the stash.** `git stash list` and find `darkfactory BL-653`; call it `stash@{N}`.
  - Untracked files: `git checkout "stash@{N}^3" -- Application/Surl.Composition.UnitLibrary/SchemeAuthenticationPolicies.cs Application/Surl.Composition.UnitLibrary/SingleSchemeServer.cs Application/Surl.Composition.UnitTests/SchemeAuthenticationPoliciesTests.cs Application/Surl.Composition.UnitTests/SingleSchemeServerTests.cs`,
    then `git reset` those paths.
  - Tracked edits, by hand from `git diff "stash@{N}^1" "stash@{N}" -- Application/Surl.Composition.UnitLibrary`:
    take only the per-scheme hunks. Those are `ExtendedProtocolServerServices.cs` (all of it),
    `AuthenticationComposition.ComposePolicy` becoming `internal`, the `SchemePolicies` member of
    `ComposedAuthentication`, and in `SurlServerComposition.cs` and `SurlServerComposition.Start.cs` the
    `authenticationPolicyForScheme`/`SchemePolicies.For` wiring.
  - Leave the mailbox hunks (`MailboxComposition`, `mailboxAccounts`, `mailDomains`, `Mailboxes`,
    `WriteStartLines`) to BL-882.
  `SurlServerComposition.cs` changed a lot on master after the stash (extended-server registrations, shared
  stores), so `git apply` will not apply cleanly. Re-make those hunks by hand.
- Stand-in (ADR-0124): accounts are the shared authentication store. Judged logins are already published by
  ADR-0128's events; this task adds no event. A refused login through a scheme's view must publish and delay
  exactly as an unknown account does, which BL-647's tests prove at the library level.

## Acceptance criteria

- [ ] `SchemeAuthenticationPoliciesTests` show `For` returning the start's policy itself when no account is
      restricted, one cached policy per scheme otherwise, and the same instance on a repeat call.
      `SingleSchemeServerTests` show the wrapper claiming only its one scheme and forwarding `ServeAsync`.
- [ ] A named test in `Application/Surl.Composition.UnitTests` calls `ComposeProtocolServers` with
      `SchemeAuthenticationPolicies.For` over a policy whose `AuthenticationSettings` hold an account restricted
      to `imap`. It builds that policy directly, because no command-line account can carry `protocols=` until
      BL-882 and BL-883. It shows `For("imap")` accepting the account and `For("smtp")` and `For("http")`
      refusing it, and that the composed IMAP and SMTP servers were given those policies (read as the existing tests read a
      composed server's fields, through `ComposedServerFields`).
- [ ] A named test shows that `scp` and `sftp` are each claimed by exactly one composed server, and the
      existing `ProtocolServerRegistrationsTests` and every other existing Composition and Console test pass.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-653 (timed out after 120 min; Stewart asked for the split 2026-11-05).
