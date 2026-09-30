---
id: BL-210
title: Prove pinned upstream curl sends mail over smtp and smtps to surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207, BL-245]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-043
created: 2026-09-29
completed: 2026-09-30
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

- [x] Integration tests exist for every case in Context and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

- 2026-09-30 (lane 6): wrote `Surl.Conformance.UnitTests/UpstreamCurlSendsMailToSurlOverSmtpTests.cs`
  (every row of ADR-0053 decision 10, 36 cases counting data rows) and `StoredMail.cs`, which
  loads the stopped surl's `--directory/.surl/mail` through `MailboxStore.LoadAsync` over a
  `DiskContentFileSystem` and fetches `INBOX`. Each stored message is checked as decision 6's
  `Return-Path` and `Received` trace fields (protocol word `ESMTP`, `ESMTPA` or `ESMTPSA`,
  date matched by pattern) followed by the body as sent, dot-unstuffed. The code was left
  uncommitted for the shift to stash, as the unattended-run rules require for a task sent back
  to Backlog.
- Against the pinned Windows reference build, 34 of 36 passed: one and two recipients, the
  invalid recipient with and without `--mail-rcpt-allowfails`, STARTTLS with `--ssl-reqd`,
  `smtps://`, no certificate (64), CRAM-MD5, PLAIN, LOGIN, XOAUTH2, OAUTHBEARER, DIGEST-MD5
  and NTLM each with and without `--sasl-ir`, PLAIN refused without TLS (67) and accepted
  with `--allow-plaintext-auth`, a wrong password (67), no login (55), VRFY, EXPN, HELP,
  NOOP, `--crlf` and bare-LF bodies, and `--max-connections 1` (8).
- The two `--max-filesize` cases fail. curl exits 8 where 55 is expected: surl advertises
  `SIZE 104857600` and accepts `MAIL ... SIZE=53` under `--max-filesize 10`, then the mail
  store refuses the body after DATA. The cause is that `ServingEngine.OpenExchange` never
  sets `ExchangeContext.Limits`, so every exchange sees `ExchangeLimits.Default`. This is
  filed as BL-245 (Surl.Core and Surl.Console, outside this task's `touches`). The expected
  result stays as ADR-0053 measured it (ADR-0003).
- 2026-09-30 (lane 3): restored lane 6's two files from the shift's stash (untracked-files
  commit `a7ab19b`) unchanged except for CRLF line endings, which `dotnet format` required.
  With BL-245 done, all 36 SMTP cases pass against the pinned Windows reference build,
  both `--max-filesize` cases included (55); `FullyQualifiedName~Surl.Conformance` is 175
  passed, 2 skipped, 0 failed, and the fast tests are green across all 30 test assemblies.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Backlog. Waits on BL-245: ServingEngine never hands --max-filesize (ExchangeLimits) to ExchangeContext, so the two max-filesize cases exit 8 not 55; 34 of 36 SMTP conformance tests pass
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned upstream curl 8.21.0 sends mail to a live surl over smtp and smtps in all 36 ADR-0053 cases, max-filesize included
