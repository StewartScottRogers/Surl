---
id: BL-1149
title: Make upstream test cases 956, 957 and 958 pass on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance, Documentation/Conformance, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1149 — Make upstream test cases 956, 957 and 958 pass on Linux and macOS

## Goal

Cases 956, 957 and 958 of upstream curl's tests (SMTP) pass on `linux-x64` and `osx-arm64` against Surl's
SMTP server half, and are in both passing lists.

## Context

- BL-1127 named 955-958; 955 now passes, 956-958 still fail in CI run 43650373643 (head `c3011bee`), found by BL-1142.
- 956: `fail protocol: line 4 differs: expected "QUIT\r\n", got "DATA\r\n"`, curl exit 0.
- 957 and 958: `fail errorcode: curl exited 0, expected 8`.
- All three show one SMTP exchange opened and ended by the server. The full blocks are in the CI artifact
  `upstream-test-case-results-linux-x64` (`upstream-test-case-failures.linux-x64.txt`); re-run via
  `gh workflow run CI --ref factory/phase-1`. Windows passes them? Check: they are on `win-x64`'s list or skipped there.
- Pinned upstream curl is the only oracle (ADR-0003); measure with `Record-CurlExchange.ps1`.

## Acceptance criteria

- [x] Cause of each of 956, 957 and 958 found and recorded under Notes, with bytes measured from pinned upstream curl.
- [ ] The three cases pass in a CI run on Linux and macOS, and their numbers are in both
      `upstream-test-cases-passing.<platform>.txt` lists with the rows in `Upstream-Test-Case-Pass-Rate.md` updated.
- [ ] `UpstreamTestCasePassRateStatementTests` passes.

## Notes

- Cause (all three): upstream `ftpserver.pl` at curl-8_21_0 (read with `gh api .../contents/tests/ftpserver.pl?ref=curl-8_21_0`) `RCPT_smtp` and `VRFY_smtp` answer `501 Invalid address` for an address with a byte past ASCII unless the case's CAPA lists `SMTPUTF8` (the regexes allow `\x{80}-\x{ff}` only then). Surl's case-mode SMTP answered RCPT (956: curl went on to DATA, exit 0) and VRFY (957, 958: `252`, exit 0) for them. 955 passed because its case scripts `REPLY MAIL 501`.
- Fix: `SmtpSession.UpstreamTestCase.cs` `IsAddressTheCaseAccepts` gates RCPT and VRFY in case mode; two unit tests added in `SmtpUpstreamTestCaseServerTests` (bytes C3 B6 / C3 A4 from the cases). Smtp tests 276/276 green, build clean. Touches widened to the Smtp projects: no other task in Doing names them.
- The three cases are `!win32`, so they skip on Windows and cannot be run here; the expected curl exit codes (55, 8) are the cases' own. Left unticked: criterion 2 needs a CI run on Linux and macOS after this commit is pushed, then adding 956-958 to both passing lists and the Pass-Rate rows. Adding them unverified would risk a red merge.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Blocked. Code fix landed; needs a CI run on Linux/macOS after the shift pushes it, then add 956-958 to both passing lists and Pass-Rate rows (cases are !win32, cannot run here)
- 2026-11-14: Blocked -> Backlog. CI run 43651751537 (PR #36, 145f16b) ran the upstream cases with this fix: 956, 957, 958, 1072 and 1073 pass on linux-x64 and osx-arm64, and both failures files are empty
- 2026-11-14: Backlog -> Doing.
