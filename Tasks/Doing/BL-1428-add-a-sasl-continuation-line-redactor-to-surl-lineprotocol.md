---
id: BL-1428
title: Add a SASL continuation-line redactor to Surl.LineProtocol
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1418]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1428 — Add a SASL continuation-line redactor to Surl.LineProtocol

## Goal

`Surl.LineProtocol` has a redactor, built on BL-1418's line redactor, that the SMTP, IMAP and POP3
servers share. It redacts a SASL initial response on the `AUTH`/`AUTHENTICATE` line, and every client
line that answers a server continuation (`334 ` in SMTP, `+ ` in IMAP and POP3), until the exchange
ends.

## Context

- Bug report item: secrets in the `-v` log. SASL PLAIN and LOGIN send the password base64-encoded,
  which is the password in clear for this purpose. Upstream curl uses an initial response where the
  server allows it (SMTP `AUTH PLAIN <b64>`, IMAP SASL-IR).
- `Protocols/Mail/Surl.LineProtocol.UnitLibrary/SaslContinuationLine.cs` already classifies
  continuation lines, and `CrlfLineReader.cs:104` uses it. The redactor needs the sent bytes to know a
  continuation was offered, which BL-1417's ADR provides for.
- CRAM-MD5, SCRAM and GSSAPI responses carry no password in clear, but redacting every continuation
  answer is simpler and loses nothing a reader needs; the ADR decides.

## Acceptance criteria

- [ ] A public redactor type (the ADR's name) in `Protocols/Mail/Surl.LineProtocol.UnitLibrary`, taking
      the command words that start SASL and the continuation prefix as parameters, so each mail server
      configures it.
- [ ] Fast tests in `Protocols/Mail/Surl.LineProtocol.UnitTests` show `AUTH PLAIN AGFsaWNlAHMzY3JldA==`
      logged in the ADR's form, an `AUTH LOGIN` exchange's two continuation answers redacted, and a `*`
      cancel line left as is.
- [ ] A client line sent when no continuation is pending is logged unchanged (named test).
- [ ] `dotnet build Protocols/Mail/Surl.LineProtocol.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

- ADR-0183 (Decided by Claude under Stewart's delegation) names the type `SaslLineRedactionRule` and decides: every continuation answer is masked, `*` and empty lines pass, IMAP literals left to BL-1430.
- Coverage collector could not be run by name here; every branch has a named test (reviewed by hand).

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
