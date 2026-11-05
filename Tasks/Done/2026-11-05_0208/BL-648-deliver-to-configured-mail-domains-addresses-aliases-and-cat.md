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
completed: 2026-11-05
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

- [x] Named tests show with a domain configured: an address in a configured domain delivers to its mailbox (case
      rules as the ADR says); an alias delivers to its target(s); a catch-all receives unknown local parts of its
      domain; an alias loop is resolved or refused as the ADR says; a recipient outside every configured domain
      gets the typed outcome the ADR names for relaying; an unknown local part with no catch-all is handled as the
      ADR says.
- [x] A named test shows the same store with no domain configured resolves exactly as ADR-0050 decision 5.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Plan: a typed `MailDomainConfiguration` (domains, mailbox addresses, `MailAlias`, `MailCatchAll`) is an optional
  `mailDomains` argument of `MailboxStore`'s constructor and `LoadAsync`; `MailDomainRouter` resolves in ADR-0102
  decision 1.3's order; `MailRecipientLookup.RelayingDenied` is the new typed outcome for relaying.
- Choices (defaults taken, within ADR-0102; no new ADR needed):
  - Start refusals are one typed exception, `MailRouteException` (`RouteKind`, `Problem`, `Address`), so BL-654
    maps it to the ADR's texts; the store knows the accounts step 3 needs, so it is the one place that can check.
  - A catch-all whose target is itself unknown in its own domain loops back to the catch-all: reported as
    `IncludesItself`, naming the target.
  - A target that is not `local@domain` (e.g. `alice@[127.0.0.1]`) is kept as written and reported as
    `TargetOutsideMailDomains`, as the ADR treats address literals.
  - `Deliver` still gives one copy per entry (existing bound tests pin it); one copy per owner applies within one
    recipient, and SMTP's per-`RCPT` deduplication must move to `OwnerNames` - noted in BL-676's Context.
  - A user account named exactly like a mailbox's address is the mailbox, not a user account, so step 3 can
    never reach a mailbox through a quoted local part.
  - `IdnMapping` verified under `InvariantGlobalization=true` (surl's published setting): U-labels convert.
- Tests: `MailboxStoreMailDomainTests`, `MailDomainConfigurationTests`, `MailDomainNameTests`,
  `MailRecipientPathTests.TryReadAddress_*`. Library 100% line and branch, worst CRAP 10.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MailboxStore resolves recipients through configured mail domains, mailboxes, aliases and catch-alls, refuses relaying with RelayingDenied, and is unchanged with no domain
