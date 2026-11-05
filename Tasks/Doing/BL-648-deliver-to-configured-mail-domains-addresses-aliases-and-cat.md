---
id: BL-648
title: Deliver to configured mail domains, addresses, aliases and catch-alls in Surl.MailStore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-642]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-648 — Deliver to configured mail domains, addresses, aliases and catch-alls in Surl.MailStore

## Goal

`MailboxStore` resolves recipients against configured mail domains, mailbox addresses, aliases and catch-alls when
any mail domain is configured, and behaves exactly as ADR-0050 says when none is.

## Context

- Decision: BL-642's ADR decision 1 (owners as full addresses, domain matching, alias and catch-all resolution
  order and loops, what is relaying, persisted owners under the old naming). ADR-0050 decisions 2 and 5 are the
  default this task must keep: with no domain configured, every existing test in
  `Protocols/Mail/Surl.MailStore.UnitTests` passes unchanged.
- Where: `Protocols/Mail/Surl.MailStore.UnitLibrary` (`MailboxStore`, `MailRecipient`, `MailRecipientLookup`,
  `MailRecipientPath`, `OwnerMailboxes`, `MailStoreIndex`). The configuration arrives as constructor input (a
  typed mail configuration; planning name `MailDomainConfiguration`), never read from a static or the disk.
- If owners' names change format in the persisted index (ADR-0050 decision 7), the ADR says how; the index bytes
  are pinned by tests.
- Out of scope: quotas, folders and shared folders (BL-675), the SMTP reply (BL-676), options (BL-653, BL-654).

## Acceptance criteria

- [ ] Named tests show with a domain configured: an address in a configured domain delivers to its mailbox (case
      rules as the ADR says); an alias delivers to its target(s); a catch-all receives unknown local parts of its
      domain; an alias loop is resolved or refused as the ADR says; a recipient outside every configured domain
      gets the typed outcome the ADR names for relaying; an unknown local part with no catch-all is handled as the
      ADR says.
- [ ] A named test shows the same store with no domain configured resolves exactly as ADR-0050 decision 5.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
