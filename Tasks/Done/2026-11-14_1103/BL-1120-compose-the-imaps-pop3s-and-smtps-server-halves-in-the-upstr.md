---
id: BL-1120
title: Compose the imaps, pop3s and smtps server halves in the upstream test-case run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md, Documentation/Conformance/upstream-test-cases-passing.win-x64.txt]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1120 — Compose the imaps, pop3s and smtps server halves in the upstream test-case run

## Goal

The upstream test-case run composes the `imaps`, `pop3s` and `smtps` server halves: the existing
IMAP, POP3 and SMTP case servers behind implicit TLS on `%IMAPSPORT`, `%POP3SPORT` and `%SMTPSPORT`.
Tests 987, 988 and 989 then stop skipping `server-half-not-composed:<name>`, pass with the Windows
reference build, and are on the `win-x64` passing list.

## Context

- FR-053 lists these three as `server-half-not-composed`. `UpstreamTestCaseServers` already marks
  `imaps`, `pop3s` and `smtps` built, and `UpstreamTestCaseVariables` already has the three port
  variables. Only the composition in `Conformance/Surl.Conformance.UnitTests` is missing.
- Follow how `ftps`, `gophers` and `mqtts` are composed: implicit TLS with the generated
  `test-localhost` certificate, on `127.0.0.1`, in `UpstreamTestCaseServerHalves`' `Listeners` table.
  Wire them to `ImapUpstreamTestCaseServer`, `Pop3UpstreamTestCaseServer` and
  `SmtpUpstreamTestCaseServer`, which share the case's one `MailUpstreamTestCaseReplies` (BL-879).
  Remove the three names from the not-composed list in `UpstreamTestCasesAgainstSurlTests`.
- The cases, at tag `curl-8_21_0`, in `UpstreamTestCases/data`: 987 (`smtps`, `--ssl-reqd`, upload
  from stdin), 988 (`imaps` `FETCH` with a quoted login), 989 (`pop3s` `RETR`). Each one's `<protocol>`
  is the plaintext command sequence, read from the case half's `ReceivedBytes` after TLS.
- The Windows reference pin lists `imaps`, `pop3s` and `smtps` (`UpstreamCurlBuilds.json`), so run
  the three cases locally with `--filter` on `UpstreamTestCasesAgainstSurlTests`. The full suite is
  not needed. Linux and macOS pick them up in BL-1121's CI run.
- Stand-in note: test composition only. The mail store is not shared here, and nothing is
  published, because each case's half is built from that case's replies alone.

## Acceptance criteria

- [x] `UpstreamTestCaseServerHalves` binds `imaps`, `pop3s` and `smtps` listeners behind implicit
      TLS, and their bound ports fill `%IMAPSPORT`, `%POP3SPORT` and `%SMTPSPORT`.
- [x] Run against the pinned Windows reference build, tests 987, 988 and 989 each end `pass` in the
      results file. The command and the three result lines are in Notes.
- [x] `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt` lists 987, 988 and 989.
      The `win-x64` row of `Upstream-Test-Case-Pass-Rate.md` gains 3 in Passed and Runnable, its
      `server-half-not-composed` skip count falls by 3, and a dated paragraph under `## Current`
      says why.
- [x] `UpstreamTestCasePassRateStatementTests` passes. `dotnet build -warnaserror` is clean and the
      fast tests are green.

## Notes

- Composed in `UpstreamTestCaseServerHalves` (`Listeners` rows and `Play` servers; `ImapsPort`, `Pop3sPort`,
  `SmtpsPort`) and `UpstreamTestCasesAgainstSurlTests` (`ServerFamilies`, the three port variables). The
  `imaps`, `pop3s`, `smtps` schemes are implicit TLS by `ListenUrl`'s own rule, so no extra TLS setup.
- Ran `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamTestCasesAgainstSurlTests.RunAsync_EveryCaseWithAComposedServerHalf"`
  (the test runs all cases; it has no per-case filter). Results: `test987 pass`, `test988 pass`, `test989 pass`.
  Windows run: 1,155 runnable, 1,154 passed, 1 failed (timeout, not a mail case), 858 skipped.
- The skipped-by-reason table's other rows were already out of step with the run; only the
  `server-half-not-composed` row and total were changed here.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. imaps, pop3s and smtps halves composed; 987-989 pass on win-x64
