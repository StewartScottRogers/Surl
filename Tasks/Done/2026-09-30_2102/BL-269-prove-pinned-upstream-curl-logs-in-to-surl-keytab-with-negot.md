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
completed: 2026-09-30
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

- [x] Every new test is `[TestCategory("Integration")]` and `[OSCondition(OperatingSystems.Windows)]`,
      and is `Inconclusive` (not failed) when the `SURL.TEST` realm mapping is absent, port 88 is
      taken, or the pinned build is not installed.
- [x] `--negotiate -u tester@SURL.TEST:<password>` fetches a file from `surl --auth negotiate
      --keytab` with exit 0 and the file's bytes on stdout; a user without an account is refused
      (curl's measured exit for a `401`).
- [x] SASL `GSSAPI` logs in over SMTP (a message stored), IMAP (a message fetched) and POP3 (a
      message retrieved) with and without `--sasl-ir`, exit 0; a user without an account exits 67.
- [x] `dotnet test --filter "TestCategory!=Integration"` stays green on every platform.

## Notes

- Filed by BL-242 (ADR-0065).
- Delivered as `UpstreamCurlLogsInToSurlWithKerberosTests` (11 cases) and the fixture
  `KerberosTestKdcOnLoopback`, which starts `Surl.Kerberos.TestKdc` in-process on
  `127.0.0.1:88` (UDP and TCP, through `Surl.Networking`'s `SocketListenerFactory`) for
  `tester@SURL.TEST` with the password `surl-test-password` and writes its keytab to a temp file.
  The test project now references `Surl.Kerberos.TestKdc.UnitLibrary`, which ADR-0065 decision 1
  allows test projects to do. All 11 passed on the lane machine, three runs in a row.
- Inconclusive checks: off Windows; no `HKLM\...\Lsa\Kerberos\Domains\SURL.TEST` key; a
  `ListenerBindException` binding port 88 (checked by holding UDP 88 while a test ran: Skipped);
  the pinned build missing (`PinnedUpstreamCurl`, as in every Conformance test).
- The class is `[DoNotParallelize]`: the solution runs tests method-level parallel and every test
  binds port 88. Each test starts its own KDC with fresh service keys; SSPI with explicit `-u`
  credentials makes its own AS and TGS exchanges each run, so no cached ticket broke a later run.
- "A user without an account" (default chosen): the KDC still issues `tester@SURL.TEST` a ticket,
  but surl's `--user-file` holds only `AccountsFile`'s `tester`, so ADR-0057 decision 10 refuses
  the principal. Measured: HTTP `--negotiate -f` exits 22 with `401`; SMTP, IMAP and POP3 exit 67.
- Mail reaches the Kerberos account's inbox only when the recipient's local part is the account
  name (ADR-0050 decision 5), so the tests send to `"tester@SURL.TEST"@example.com`: the pinned
  build passes the quoted local part through and surl unquotes it. The IMAP and POP3 cases seed
  the inbox with a GSSAPI SMTP delivery first, then fetch or retrieve it with GSSAPI and compare
  it with the stored message.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned Windows curl logs in to surl --keytab with --negotiate over HTTP and SASL GSSAPI over SMTP, IMAP and POP3 through the test KDC; 11 Integration tests
