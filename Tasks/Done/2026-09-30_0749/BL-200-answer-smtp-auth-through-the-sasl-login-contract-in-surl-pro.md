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
completed: 2026-09-30
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

- [x] A fast test replays each BL-185 SMTP fixture with the double scripted to accept, and
      asserts surl's replies byte for byte.
- [x] Fast tests cover: a refused login (`535` or the ADR's code) and the login note; a
      plain-text mechanism over no TLS not advertised and refused as the ADR says; `*` cancel;
      invalid base64; `AUTH` twice; `AUTH` after `MAIL`; `MAIL` refused before login where the ADR
      requires one.
- [x] `dotnet build Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- The server takes a second policy, `IMailAuthenticationPolicy`, beside `IAuthenticationPolicy`,
  rather than casting one to the other: the tests can script each alone, and `Surl.Console`
  (BL-207) passes the same object twice. Nothing in `Surl.Console` constructs the SMTP server
  yet, so the constructor change stays inside this task's projects.
- Every reply is ADR-0049 section 7's and ADR-0053 decision 1's; no new decision was needed, so
  no ADR. A logged-in session skips the no-credentials `MAIL` check, and `STARTTLS` logs it out
  (RFC 3207: the session starts over).
- The 16 `auth-*` fixtures were recorded from pinned upstream curl 8.21.0 with
  `Record-CurlExchange.ps1 -Smtp -SaslChallenge`; the recipe is in the fixtures' README, and
  re-recording `auth-plain`, `auth-login-sasl-ir`, `auth-external` and
  `auth-xoauth2` reproduced `request.bin` and `transcript.txt` byte for byte.
- The three enum maps (`SmtpReplies.LoginEnded`, `SmtpReplies.SaslExchangeAbandoned`,
  `SmtpSession.AsLineReadOutcome`) are tested over every enum value, so their fall-through arms
  are covered; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary`: 100% line,
  100% branch, no failing member.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SMTP AUTH answered through the SASL contract; fixtures replayed byte for byte; 100% coverage
