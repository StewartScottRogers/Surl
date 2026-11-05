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
completed: 2026-11-05
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

- [x] Named tests replay a session with a configured domain: a recipient in the domain accepted and delivered; an
      alias and a catch-all delivered as the store resolves; a recipient outside every configured domain answered
      with the ADR's measured reply and not delivered; a message to one accepted and one refused recipient
      delivered to the accepted one only.
- [x] Every existing test in `Protocols/Mail/Surl.Protocol.Smtp.UnitTests` passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Reply and exit codes are ADR-0102 decision 1.1's measurement of pinned upstream curl 8.21.0 (rows R1-R4):
  `550 5.7.1 Relaying denied` at `RCPT`; curl exits 55 unless `--mail-rcpt-allowfails`, which sends `DATA` to
  the others (R3). No new measurement was needed; the tests replay those sessions' bytes.
- Order at `RCPT` (sensible default taken): invalid path `501` first, then a parameter `555`, then relaying
  `550`, then the 100-recipient `452`. A relaying refusal is not counted, so `DATA` with no accepted recipient is
  still `503 Send RCPT first`. It is noted: `RCPT <path> refused: outside every --mail-domain`.
- A domainless path (`<ann>`) is still retried as `ann@surl` (ADR-0053 decision 4), which is `RelayingDenied`
  with a domain configured - the ADR's "bare local part is relaying" - unless an operator configures
  `--mail-domain surl` itself, an edge left as is.
- One copy per owner: `SmtpMailTransaction` now drops a recipient whose owners were all reached and lets a new
  one replace kept recipients it covers (so `bob` then `team` gives bob one copy). A `MailRecipient` cannot be
  split outside `Surl.MailStore.UnitLibrary` (BL-675 holds it), so two partly overlapping aliases still double
  the shared owner's copy; filed BL-763 (depends on BL-675) for the store-side merge.
- Tests: `SmtpMailDomainTests` (10 new; 228 in the project). Library coverage 100% line, 100% branch.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. SMTP refuses recipients outside every --mail-domain with 550 5.7.1 Relaying denied and delivers aliases and catch-alls once per owner
