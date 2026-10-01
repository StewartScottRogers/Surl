---
id: BL-211
title: Prove pinned upstream curl reads mail over imap and imaps from surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-208, BL-210]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-044
created: 2026-09-29
completed: 2026-09-30
---
# BL-211 — Prove pinned upstream curl reads mail over imap and imaps from surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned
upstream curl 8.21.0 builds read, search, append and manage mail on a live `surl` over `imap`
and `imaps` as BL-187's and BL-185's ADRs expect, including mail another curl delivered over
SMTP to the same `surl`.

## Context

- The cases: BL-187's ADR's list, covering at least the mailbox list, a fetch by `UID`, by
  `MAILINDEX`, a `SECTION` and a `PARTIAL`, a search, a `-T` append, `-X` commands, `LOGIN` and
  each `AUTHENTICATE` mechanism BL-185's ADR offers, `--ssl-reqd` (`STARTTLS`) and `imaps://`
  with `--self-signed` (curl `-k`), a plain-text `LOGIN` refused without TLS and accepted with
  `--allow-plaintext-auth`; and a round trip: curl sends over `smtp` (BL-210's case), then curl
  fetches the same message over `imap` from the same `surl`, byte for byte.
- Harness as BL-210; Linux and macOS legs on CI.
- Any disagreement with the pinned build is fixed through a new task filed by `task-planner`,
  never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [x] Integration tests exist for every case in Context and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

- `UpstreamCurlReadsMailFromSurlOverImapTests` (44 cases) covers every row of ADR-0055 decision
  15. Each case starts one surl listening on `smtp` beside `imap` or `imaps` over a fresh
  `--directory`, has the pinned curl deliver `mail.txt` to `tester` over `smtp` first, and
  compares every whole-message fetch byte for byte with what the stopped surl's persisted store
  holds (the trace fields, then `mail.txt`). The round trip is proven on every such fetch.
- `SurlOnLoopback` can now start one surl on several schemes (`StartOverDirectoryAsync(schemes,
  ...)`, `BaseUrls`, `BaseUrlOf`), waiting for one status line per listener. The same process is
  what makes it a round trip through the same `surl`.
- Choice: accounts come from `--user-file` (`AccountsFile`: `tester:secret` and the bearer
  token) in place of the ADR's `--user tester:secret`. It is the same account, and the file
  also carries the bearer token the `XOAUTH2`/`OAUTHBEARER` rows need.
- Choice: the `--max-filesize 10` `APPEND` row skips the SMTP seeding, because the same limit
  refuses that delivery (552). The row proves only the `APPEND` refusal.
- The clear-password `LOGIN` command is driven with curl's `--login-options AUTH=+LOGIN`. It is
  refused over plaintext (curl sees `LOGINDISABLED`, exit 67), accepted with
  `--allow-plaintext-auth`, and accepted over `imaps`.
- Pinned Windows build: every case agreed with the ADR; no disagreement was found, so no task
  was filed. Linux and macOS legs run on CI.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned upstream curl reads, searches, appends and manages mail on surl over imap and imaps, including mail curl sent over smtp, byte for byte
