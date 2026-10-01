---
id: BL-269
title: Prove pinned upstream curl logs in to surl --keytab with Negotiate and SASL GSSAPI
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-265, BL-266, BL-268]
touches: [Surl.Conformance.UnitTests]
requirement: FR-046
created: 2026-09-30
completed:
---
# BL-269 — Prove pinned upstream curl logs in to surl --keytab with Negotiate and SASL GSSAPI

## Goal

Integration tests show the pinned Windows reference build logging in to a started
`surl --keytab` with `--negotiate` over HTTP and with SASL `GSSAPI` over SMTP, IMAP and POP3,
through the hand-built test KDC.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md` decision 3; ADR-0057 decisions 8 to 10 (as BL-268 leaves them).
- Beside the other `UpstreamCurlLogsInToSurl*` tests in `Surl.Conformance.UnitTests`; the test
  KDC is `Surl.Kerberos.TestKdc.UnitLibrary` (BL-266), started in-process on `127.0.0.1:88`.
- URLs name `web.surl.test` / `mail.surl.test` with `--resolve ...:127.0.0.1`; accounts named
  `tester@SURL.TEST` (ADR-0057 decision 10).

## Acceptance criteria

- [ ] Every new test is `[TestCategory("Integration")]` and `[OSCondition(OperatingSystems.Windows)]`,
      and is `Inconclusive` (not failed) when the `SURL.TEST` realm mapping is absent, port 88 is
      taken, or the pinned build is not installed.
- [ ] `--negotiate -u tester@SURL.TEST:<password>` fetches a file from `surl --auth negotiate
      --keytab` with exit 0 and the file's bytes on stdout; a user without an account is refused
      (curl's measured exit for a `401`).
- [ ] SASL `GSSAPI` logs in over SMTP (a message stored), IMAP (a message fetched) and POP3 (a
      message retrieved) with and without `--sasl-ir`, exit 0; a user without an account exits 67.
- [ ] `dotnet test --filter "TestCategory!=Integration"` stays green on every platform.

## Notes

- Filed by BL-242 (ADR-0065).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
