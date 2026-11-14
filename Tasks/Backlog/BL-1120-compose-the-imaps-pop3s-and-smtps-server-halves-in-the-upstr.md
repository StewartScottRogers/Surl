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
completed:
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

- [ ] `UpstreamTestCaseServerHalves` binds `imaps`, `pop3s` and `smtps` listeners behind implicit
      TLS, and their bound ports fill `%IMAPSPORT`, `%POP3SPORT` and `%SMTPSPORT`.
- [ ] Run against the pinned Windows reference build, tests 987, 988 and 989 each end `pass` in the
      results file. The command and the three result lines are in Notes.
- [ ] `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt` lists 987, 988 and 989.
      The `win-x64` row of `Upstream-Test-Case-Pass-Rate.md` gains 3 in Passed and Runnable, its
      `server-half-not-composed` skip count falls by 3, and a dated paragraph under `## Current`
      says why.
- [ ] `UpstreamTestCasePassRateStatementTests` passes. `dotnet build -warnaserror` is clean and the
      fast tests are green.

## Notes

## Log

- 2026-11-14: Created.
