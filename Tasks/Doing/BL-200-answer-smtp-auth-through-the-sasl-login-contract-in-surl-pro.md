---
id: BL-200
title: Answer SMTP AUTH through the SASL login contract in Surl.Protocol.Smtp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-199, BL-193]
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-200 — Answer SMTP AUTH through the SASL login contract in Surl.Protocol.Smtp

## Goal

`SmtpProtocolServer` answers `AUTH` (RFC 4954) through BL-193's SASL contract - advertising the
mechanisms BL-185's ADR allows in the current TLS state, relaying `334` continuations, taking an
initial response, answering success and failure - and lets a logged-in client submit mail.

## Context

- Decisions: BL-185's ADR (mechanisms per TLS state, plain-text rule, failure codes); BL-186's
  ADR (what a login unlocks); ADR-0032 "Protocol servers not yet built" criteria 1 to 3;
  ADR-0038 (write the `CheckedLogin` note).
- The server decides nothing about credentials: it frames the exchange (`334 <base64>`,
  `235`, `535`, `*` cancels with `501`), BL-192's base64 continuation helper, and asks the
  contract. Tests use `AnonymousAuthenticationPolicy` and a scripted double of BL-193's
  interface; never `Surl.Authentication` (ADR-0002).
- Fixtures: BL-185's SMTP recordings (each mechanism, with and without `--sasl-ir`).

## Acceptance criteria

- [ ] A fast test replays each BL-185 SMTP fixture with the double scripted to accept, and
      asserts surl's replies byte for byte.
- [ ] Fast tests cover: a refused login (`535` or the ADR's code) and the login note; a
      plain-text mechanism over no TLS not advertised and refused as the ADR says; `*` cancel;
      invalid base64; `AUTH` twice; `AUTH` after `MAIL`; `MAIL` refused before login where the ADR
      requires one.
- [ ] `dotnet build Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
