---
id: BL-210
title: Prove pinned upstream curl sends mail over smtp and smtps to surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-043
created: 2026-09-29
completed:
---
# BL-210 — Prove pinned upstream curl sends mail over smtp and smtps to surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned
upstream curl 8.21.0 builds send mail to a live `surl` over `smtp` and `smtps` as BL-186's and
BL-185's ADRs expect, and that the mail lands in the store.

## Context

- The cases: BL-186's ADR's list, covering at least one and two recipients, a refused recipient
  with and without `--mail-rcpt-allowfails`, `--ssl-reqd` (`STARTTLS`) and `smtps://` with
  `--self-signed` (curl `-k`), a login with each SASL mechanism BL-185's ADR offers (forced with
  `--login-options AUTH=<mech>`, with and without `--sasl-ir`), a plain-text login refused over
  `smtp://` without TLS (curl's exit code, measured) and accepted with `--allow-plaintext-auth`,
  a message past `--max-filesize`, and `-X VRFY`.
- The stored message is read back from a temporary `--directory` through the persisted format
  (BL-184's ADR), so this task does not wait on IMAP or POP3.
- Harness: `Surl.Conformance.UnitTests/SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`,
  `AccountsFile.cs`, `TestCertificateAuthority.cs`; `Assert.Inconclusive` when the pinned build is
  absent; Linux and macOS legs on CI (ADR-0016).
- Any disagreement with the pinned build is fixed in the library at fault through a new task
  filed by `task-planner`, never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [ ] Integration tests exist for every case in Context and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
