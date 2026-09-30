---
id: BL-199
title: Answer SMTP STARTTLS and implicit smtps in Surl.Protocol.Smtp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-198]
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests]
requirement: FR-043
created: 2026-09-29
completed:
---
# BL-199 — Answer SMTP STARTTLS and implicit smtps in Surl.Protocol.Smtp

## Goal

`SmtpProtocolServer` answers `STARTTLS` (RFC 3207) and serves implicit `smtps://`, with the
capability list and session state BL-186's ADR gives for each TLS state, so
`curl --ssl-reqd smtp://...` and `curl smtps://...` can deliver.

## Context

- Decisions: BL-186's ADR (replies, capabilities after TLS, `STARTTLS` without a certificate);
  ADR-0010 (the server calls `IConnection.UpgradeToTlsAsync` after `220`, discarding every byte
  read past the `STARTTLS` line - BL-192's helper); RFC 3207 section 4.2 (the session resets
  after the upgrade: the client must `EHLO` again); ADR-0032 section 10 (no certificate: the
  upgrade refused in SMTP's own words).
- `smtps`: the engine completes the implicit handshake before `ServeAsync`, so the server claims
  `smtps` too (or `Surl.Console` wraps it, as BL-207 decides); record which in Notes.
- Tests: `InMemoryConnection`'s upgrade simulation (ADR-0010); fixtures from BL-186's
  `--ssl-reqd` and `smtps://` recordings.

## Acceptance criteria

- [ ] A fast test replays the `--ssl-reqd` and `smtps://` fixtures and asserts surl's replies and
      upgrade point.
- [ ] Fast tests cover: bytes pipelined after `STARTTLS` discarded, never run; `STARTTLS` twice;
      `STARTTLS` with no certificate; a failed upgrade; `MAIL` before the new `EHLO`; the
      capability list before and after TLS.
- [ ] `dotnet build Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
