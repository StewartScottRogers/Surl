---
id: BL-676
title: Refuse SMTP recipients outside the configured mail domains
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-648]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-676 — Refuse SMTP recipients outside the configured mail domains

## Goal

With a mail domain configured, the SMTP server answers each `RCPT TO` as BL-642's ADR says - delivering to
configured addresses, aliases and catch-alls through `MailboxStore`, and refusing a recipient outside every
configured domain (relaying) with the measured reply - and with none configured it answers exactly as today.

## Context

- Decision: BL-642's ADR decision 1 (relaying refusal, unknown local parts) with its measurement of pinned
  upstream curl 8.21.0 (the reply code and text at `RCPT`, curl's exit code, and `--mail-rcpt-allowfails`).
  ADR-0053 is how the SMTP server answers today; ADR-0050 decision 5 is the default.
- Where: `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary`, calling BL-648's recipient resolution. Reply lines go
  through `Surl.LineProtocol`'s reply-line writer.
- From BL-648: `LookUpRecipient` returns `MailRecipientLookup.RelayingDenied` for a recipient outside every
  configured domain, and an alias's `MailRecipient` may name several owners (`OwnerNames`; `OwnerName` is only the
  first). `SmtpMailTransaction` deduplicates by `OwnerName` today; with aliases it must deduplicate by every name
  in `OwnerNames` (one copy per owner per message, ADR-0102 decision 1.3), or two `RCPT`s whose aliases overlap
  lose or double a copy. `MailboxStore.Deliver` gives one copy per owner of each entry.
- Oracle: pinned upstream curl only (ADR-0003); unit tests replay the bytes curl sends, as the existing SMTP tests
  do. `UpstreamCurlSendsMailToSurlOverSmtpTests` must still pass unchanged.

## Acceptance criteria

- [ ] Named tests replay a session with a configured domain: a recipient in the domain accepted and delivered; an
      alias and a catch-all delivered as the store resolves; a recipient outside every configured domain answered
      with the ADR's measured reply and not delivered; a message to one accepted and one refused recipient
      delivered to the accepted one only.
- [ ] Every existing test in `Protocols/Mail/Surl.Protocol.Smtp.UnitTests` passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
