---
id: BL-1291
title: Record the Linux and macOS mail conformance results against ADR-0053, ADR-0055 and ADR-0056
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1289]
touches: [Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0056-how-the-pop3-server-answers-upstream-curl.md]
requirement: FR-046
created: 2026-11-16
completed: 2026-11-16
---
# BL-1291 — Record the Linux and macOS mail conformance results against ADR-0053, ADR-0055 and ADR-0056

## Goal

The SMTP, IMAP and POP3 ADRs each state what the Linux and macOS reference builds of upstream curl 8.21.0 (OpenSSL) did against `surl` in BL-1289's CI run, row for row where it differs from Windows.

## Context

- ADR-0053 (just before "Alternatives considered"), ADR-0055 and ADR-0056 each say: "The rows are pinned from this ADR's measurements; where the Linux or macOS OpenSSL builds give another exit ... it is pinned per platform in its own test (root `CLAUDE.md`) and recorded against this ADR." Nothing is recorded yet; the Roadmap's Milestone 3 is "not closed" for that reason.
- The measurement is in `Documentation/Conformance/Conformance-Off-Windows.md` (BL-1289): the outcomes of `UpstreamCurlSendsMailToSurlOverSmtpTests`, `UpstreamCurlReadsMailFromSurlOverImapTests`, `UpstreamCurlReadsMailFromSurlOverPop3Tests`, `UpstreamCurlSharesMailAcrossSurlProtocolsTests` and `UpstreamCurlLogsInToSurlWithKerberosTests` on each platform.
- Where a test has a per-platform twin (`[OSCondition]`), name the twin and the exit it pins. Where a test is Inconclusive off Windows, state why from the `.trx` message.
- Record, do not decide. A failure becomes a filed task (ID in Notes), not a code change here.
- Stand-in: not applicable; a record of upstream curl's results.

## Acceptance criteria

- [x] ADR-0053, ADR-0055 and ADR-0056 each have a short section or amendment, "Measured on Linux and macOS", naming BL-1289's CI run and the reference pins' SHA-256.
- [x] Each of those sections states how many of the ADR's conformance tests passed, were Inconclusive and failed on `linux-x64` and on `osx-arm64`, matching `Conformance-Off-Windows.md`.
- [x] Each test that pins a platform-specific exit, or that is Inconclusive off Windows, is named with its exit or its reason.
- [x] A task is filed for each failed mail test, its ID in Notes, or Notes says none failed.

## Notes

No mail test failed on either platform, so no task was filed. Each ADR got a "Measured on Linux and macOS" section: 36 (SMTP), 44 (IMAP), 41 (POP3) passed, 0 Inconclusive, 0 failed on both platforms; no per-platform twins exist because no exit differs. The Kerberos GSSAPI tests are Windows-only skips, noted in each section.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ADR-0053, 0055 and 0056 record the Linux and macOS mail conformance results from BL-1289's CI run
