---
id: BL-1386
title: Document how seed_mail resolves its recipient
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp/README.md]
requirement: none
created: 2026-11-18
completed:
---
# BL-1386 — Document how seed_mail resolves its recipient

## Goal

`surl-mcp`'s `seed_mail` tool description and `Application/Surl.Mcp/README.md` say how the `to` address finds its
account: as SMTP's `RCPT TO` does, so without a mail domain only the local part counts, matched exactly or else
ignoring case, and the domain is ignored.

## Context

- The closing question of the Surly cookbook bug report of 2026-11-18: `seed_mail` with `ALICE@example.com` and with
  `alice@other.org` both deliver to account `alice`. The reporter asked whether that is intended.
- It is the SMTP rule, not a separate one: `SeedMailTool` (`Application/Surl.Mcp.UnitLibrary/SeedMailTool.cs:42-51`)
  calls `Surl.Api`'s `Mail.DeliverAsync`, which reaches `MailboxStore.Seed`
  (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.Control.cs:26-30`), which resolves the recipient with
  `LookUpRecipient` (`MailboxStore.cs:323-345`) - the same call SMTP's `RCPT TO` makes (`SmtpSession.cs:275`). Under
  `--allow-anonymous` every valid recipient goes to the one anonymous mailbox set; otherwise, without
  `--mail-domain`, only the local part counts and an account is matched ordinally or else as the one account equal
  to it ignoring case (`LocalPartLookup.cs`, ADR-0050 decision 5); with `--mail-domain`, ADR-0102 decision 1.1's
  routing applies (alias, mailbox, account by local part, catch-all; a recipient outside every domain is refused
  `RelayingDenied`). `surl --aihelp mail` already states the `--mail-domain` half (`AiHelpProse.cs:223`) and the
  SMTP topic the local-part half (`AiHelpProse.cs:389`), so `--aihelp` needs no change.
- The description is the `Description` constant (`SeedMailTool.cs:16-19`); the README's tool list is
  `Application/Surl.Mcp/README.md:31-35`.
- Stand-in: documentation for agents seeding the shared mail store; no behaviour changes.

## Acceptance criteria

- [ ] `SeedMailTool.Description` says the recipient is resolved as SMTP's `RCPT TO` is, and that without a mail
      domain only the local part counts (case ignored when only one account matches) and the domain is ignored.
- [ ] `SeedMailTool`'s XML doc says the same, citing ADR-0050 decision 5 and ADR-0102 decision 1.1.
- [ ] `Application/Surl.Mcp/README.md` states the rule beside `seed_mail`, with the `ALICE@example.com` /
      `alice@other.org` example.
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean and the fast tests are green
      (`ProgramTests` and any test pinning tool descriptions still pass).

## Notes

- `align-and-document` only. The same rule for `Surl.Api`'s `Mail.DeliverAsync` is documented by BL-1384.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
