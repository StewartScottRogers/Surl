---
id: BL-212
title: Prove pinned upstream curl reads mail over pop3 and pop3s from surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-209, BL-210]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-045
created: 2026-09-29
completed: 2026-09-30
---
# BL-212 — Prove pinned upstream curl reads mail over pop3 and pop3s from surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned
upstream curl 8.21.0 builds list, retrieve and delete mail on a live `surl` over `pop3` and
`pop3s` as BL-188's and BL-185's ADRs expect, including mail another curl delivered over SMTP to
the same `surl`.

## Context

- The cases: BL-188's ADR's list, covering at least `LIST`, `RETR`, `-l`, `-I`, `-X UIDL`,
  `-X 'DELE 1'` followed by a `LIST` showing it gone, `USER`/`PASS`, `APOP`
  (`--login-options AUTH=+APOP`) and each SASL mechanism BL-185's ADR offers, `--ssl-reqd`
  (`STLS`) and `pop3s://` with `--self-signed` (curl `-k`), a plain-text `PASS` refused without
  TLS and accepted with `--allow-plaintext-auth`; and a round trip: curl sends over `smtp`, then
  curl retrieves the same message over `pop3`, byte for byte.
- Harness as BL-210; Linux and macOS legs on CI.
- Any disagreement with the pinned build is fixed through a new task filed by `task-planner`,
  never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [x] Integration tests exist for every case in Context and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

- `UpstreamCurlReadsMailFromSurlOverPop3Tests` (41 cases, `[TestCategory("Integration")]`) covers
  every row of ADR-0056 decision 12 against the Windows reference build; all 41 pass first time,
  so no disagreement with the pinned build was found and no fix task was filed. The whole
  Conformance run is 372 passed, 5 skipped (the pre-existing platform skips).
- Harness as BL-210 and the IMAP twin (`UpstreamCurlReadsMailFromSurlOverImapTests`): one
  in-process surl on `smtp` beside `pop3`/`pop3s`, curl delivers `mail.txt` over `smtp`, then
  reads it back; every retrieval is compared byte for byte with the message the stopped surl's
  persisted store holds (the SMTP round trip). Linux and macOS go Inconclusive without a pinned
  build, as every conformance class does.
- Choice: rows whose surl options include `--auth` (`basic`, `apop`, `digest-md5`, `ntlm`) have
  their message delivered by an earlier surl over the same `--directory` with the default login
  options, because an `--auth` set without a mail mechanism leaves SMTP nothing curl would log in
  with; the persisted store carries the message across (ADR-0050 decision 7).
- `StoredMail` gained `ReadInboxUidValidityAsync`, so the `UIDL` row checks the unique-id against
  the store's own `UIDVALIDITY` rather than a pattern.
- The `[IN-USE]` row holds the maildrop with a raw `USER`/`PASS` session (surl given
  `--allow-plaintext-auth`), while curl logs in with its default `CRAM-MD5`.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. 41 integration tests prove pinned curl 8.21.0 lists, retrieves and deletes SMTP-delivered mail over pop3 and pop3s from surl as ADR-0056 decision 12 expects
