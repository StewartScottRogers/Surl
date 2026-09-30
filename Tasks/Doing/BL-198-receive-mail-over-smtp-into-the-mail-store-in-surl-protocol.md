---
id: BL-198
title: Receive mail over SMTP into the mail store in Surl.Protocol.Smtp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-186, BL-190, BL-192]
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests]
requirement: FR-043
created: 2026-09-29
completed:
---
# BL-198 — Receive mail over SMTP into the mail store in Surl.Protocol.Smtp

## Goal

`Surl.Protocol.Smtp` has an `SmtpProtocolServer` (`IConnectionProtocolServer`, scheme `smtp`)
that greets, answers `EHLO`/`HELO`, `MAIL`, `RCPT`, `DATA`, `RSET`, `NOOP`, `VRFY`, `EXPN`,
`HELP` and `QUIT`, and delivers each accepted message into the shared mail store, as BL-186's
ADR decides (TLS is BL-199, `AUTH` is BL-200).

## Context

- Decisions: BL-186's ADR (every reply, capability list, whether a login is needed before
  `MAIL`, recipient answers); BL-184's ADR (delivery and bounds); ADR-0006 section 5's SMTP
  column (`421` for timeouts, `500` for a line past `--max-line`, `552` for a message past
  `--max-filesize` with the partial message discarded); ADR-0006 section 3.
- Until BL-200, a server that BL-186's ADR says needs a login before `MAIL` refuses `MAIL` unless
  the policy is `--allow-anonymous`; tests use `AnonymousAuthenticationPolicy`.
- Libraries: add `ProjectReference`s to `Surl.MailStore.UnitLibrary` (BL-190's store, taken in
  the constructor) and `Surl.LineProtocol.UnitLibrary` (BL-192's line reader and dot-unstuffing),
  as BL-184's ADR allows.
- Seam: ADR-0004 (`InMemoryConnection`); fixtures from BL-186's recordings under
  `Surl.Protocol.Smtp.UnitTests/Fixtures/<case>/` with a `README.md` (build, SHA-256, command
  line, date).
- Code to copy (never expectations): the Curl port's `SmtpReply.cs`, `SmtpDotStuffer.cs`,
  turned to the server's side.

## Acceptance criteria

- [ ] A fast test replays each BL-186 fixture that needs neither TLS nor `AUTH` (one recipient,
      two recipients, a refused recipient, `VRFY`, `EXPN`, `HELP`, `NOOP`, a dot-stuffed body) and
      asserts surl's replies are the ADR's, byte for byte, and the stored message's bytes.
- [ ] Fast tests cover: `DATA` before `RCPT`; `MAIL` with a `SIZE` over `--max-filesize`; a body
      past `--max-filesize` (`552`, nothing stored); a line past `MaxLineBytes`; the head timeout
      on a fake `TimeProvider`; the store's bound reached.
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Smtp.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no `Integration` test in `Surl.Protocol.Smtp.UnitTests` and
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
