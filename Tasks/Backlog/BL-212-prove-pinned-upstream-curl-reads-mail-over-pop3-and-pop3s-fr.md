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
completed:
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

- [ ] Integration tests exist for every case in Context and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

## Log

- 2026-09-29: Created.
