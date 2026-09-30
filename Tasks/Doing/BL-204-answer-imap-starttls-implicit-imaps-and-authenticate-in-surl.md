---
id: BL-204
title: Answer IMAP STARTTLS, implicit imaps and AUTHENTICATE in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-201, BL-193]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-204 — Answer IMAP STARTTLS, implicit imaps and AUTHENTICATE in Surl.Protocol.Imap

## Goal

`ImapProtocolServer` answers `STARTTLS`, serves implicit `imaps://`, and answers `AUTHENTICATE`
(with `SASL-IR`) through BL-193's SASL contract, with the capabilities BL-187's and BL-185's
ADRs give for each state.

## Context

- Decisions: BL-187's ADR (capabilities per state, `STARTTLS` without a certificate); BL-185's
  ADR (mechanisms per TLS state, plain-text rule, failures); ADR-0010 (upgrade by the server,
  bytes after the `STARTTLS` line discarded - BL-192's helper); ADR-0032 section 10 and criteria
  1 to 3; ADR-0038 (the login note); RFC 3501 section 6.2.1 and 6.2.2, RFC 4959 (`SASL-IR`).
- `imaps`: claimed by the server or wrapped by `Surl.Console` (BL-208); record which in Notes.
- Tests: `InMemoryConnection`'s upgrade simulation; a scripted double of BL-193's interface;
  BL-185's IMAP recordings and BL-187's `--ssl-reqd` and `imaps://` recordings.

## Acceptance criteria

- [ ] A fast test replays each fixture named in Context and asserts surl's responses byte for
      byte.
- [ ] Fast tests cover: bytes pipelined after `STARTTLS` discarded; `STARTTLS` with no
      certificate; capabilities before and after TLS and after login; `AUTHENTICATE` accepted,
      refused (with the note) and cancelled with `*`; a plain-text mechanism over no TLS refused as
      the ADR says; `LOGIN` accepted after TLS.
- [ ] `dotnet build Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
