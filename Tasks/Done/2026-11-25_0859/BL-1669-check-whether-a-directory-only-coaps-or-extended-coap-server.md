---
id: BL-1669
title: Check whether a directory-only CoAPS or extended CoAP server answers unauthenticated reads openly
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions, Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-1669 — Check whether a directory-only CoAPS or extended CoAP server answers unauthenticated reads openly

## Goal

A `coap` or `coaps` server whose only account source is a `--user-ldap` directory serving that scheme answers an
unauthenticated read with `4.01`, as it does with one local account.

## Context

- Filed from BL-1663, which fixed the same hole for the HTTP family. ADR-0149 amendment 1, ADR-0097 decision 6,
  ADR-0032 section 4, ADR-0102 decision 4.3 (a scheme and its TLS twin are one `protocols=` entry).
- `ExtendedProtocolServers.CoapAccessRulesFor` (`Application/Surl.Composition.UnitLibrary`) asks only the account
  book whether the server is private.

## Acceptance criteria

- [x] A named test in `Application/Surl.Composition.UnitTests/CoapsCompositionTests.cs` shows a directory-only
      policy yields `CoapAccessRules` whose `MayRead(false)` is false, with `protocols=` absent, `coap` and `coaps`.
- [x] A named test shows a directory whose `protocols=` admits neither `coap` nor `coaps` leaves reads open, and
      `--allow-anonymous` with a directory still serves reads.
- [x] Named tests in `Surl.Authentication.UnitTests` show `LdapAccountSource.Serves` admits a TLS twin, and an
      HTTP session over a directory with `protocols=https` challenges an unauthenticated read.
- [x] ADR-0149 states the rule for CoAP and the twin rule in an amendment, and `--aihelp` and `--manual` say a
      directory counts as an account.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and every member this
      task changes keeps 100% line and branch coverage.

## Notes

- Answer to the title: yes, it was open. A directory-only `coap`/`coaps` server served unauthenticated reads.
- Decided by Claude under Stewart's delegation (ADR-0149 amendment 2): a directory serving `coap` or `coaps` makes
  CoAP private; `protocols=` on `--user-ldap` admits TLS twins; the HTTP family asks the directory about its
  server's own scheme, not `http`.
- Fixed here rather than filed: the task's question is a security hole, and the fix is small. The twin and per-view
  rules were needed with it: without twins `protocols=coaps` left `coap` open, and with twins alone `protocols=https`
  would have admitted directory logins on `ws` and `rtsp` (found by the code review).
- Tests: `CoapsCompositionTests.CoapAccessRulesFor_Directory*`, `AuthenticationSettingsTests`,
  `LdapAccountSourceTests.Serves_FollowsTheProtocolsKey`, `HttpAuthenticationSessionTests.JudgeAsync_Directory*` and
  `JudgeAsync_BasicLogin_AsksTheDirectoryOnlyOnASchemeItServes`. Each new behaviour test failed before its fix.
- `touches` was empty; it now lists the projects changed. `Surl.Extended.Protocol.Coap.UnitLibrary` changed in one
  doc comment. No other task was in Doing.
- Coverage: `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 0 failing members; the one
  composition member changed (`CoapAccessRulesFor`) has no branch and is run by the new tests.
- `dotnet format --verify-no-changes` passes on the changed files; it reports line-ending and import-order errors in
  files this task did not change.
- No upstream curl measurement: upstream curl has no CoAP, and the `401` and `4.01` bytes are unchanged.
- Filed: BL-1683 (CoAP rules read once, High), BL-1684 (SSH asks the directory about `ssh`).

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A directory-only coap or coaps server answers unauthenticated reads 4.01, and --user-ldap protocols= is judged by each server's scheme and its TLS twin
