---
id: BL-1384
title: Correct the README share example and the Surl.Api README's flags, namespaces and event cast
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md, Application/Surl.Api/README.md]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-1384 — Correct the README share example and the Surl.Api README's flags, namespaces and event cast

## Goal

The root `README.md`'s "Configuring a real server" example gives a share grant someone can actually use over HTTP,
and `Application/Surl.Api/README.md` names mail flags as the build returns them, the namespaces of `StoreKind` and
`StoreChangedEvent`, the cast a `WaitForEventAsync` caller needs, and how `Mail.DeliverAsync` finds its recipient.

## Context

- Finding 6 (first three items) of the Surly cookbook bug report of 2026-11-18, plus the closing question as it
  applies to `Surl.Api`.
- `README.md:387-427`: the `surl.cfg` example grants share `docs` to `ann@example.com:r`, a `--mailbox` account.
  A mailbox's account logs in only on the mail schemes unless its `protocols=` says otherwise (ADR-0102 decision 1.4,
  "`protocols=imap,ftp` opens FTP on purpose"; `MailboxComposition` restricts it), so over HTTP nobody can read
  `docs`: her login and an anonymous request both get 404. Fix the example (grant a `--user` account, or give the
  mailbox `;protocols=smtp,imap,pop3,http`) or say plainly that a mailbox's account reaches a share only on the
  schemes its `protocols=` names. The paragraph after the example says the file was run on 2026-11-12; keep that
  sentence true.
- `Application/Surl.Api/README.md:297`: "Flags are IMAP names (`Seen`)". The build returns `\Seen`, the RFC 9051
  system flag with its backslash; the README changes, not the code.
- `Application/Surl.Api/README.md:10-13` shows `using Surl.Protocol.Abstractions;   // SurlExitCode`, but the
  example at lines 92-96 uses `StoreKind` and the events section uses `StoreChangedEvent`, both also in
  `Surl.Protocol.Abstractions` (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs`,
  `StoreChangedEvent.cs`). `SurlServer.WaitForEventAsync` returns the base `SurlEvent`
  (`Application/Surl.Api/SurlServer.cs:139`); a `StoreChangedEvent`'s path is its `Name`
  (`StoreChangedEvent.cs:13-18`), so reading it needs a cast or pattern match the README does not show. (The report
  calls it `Path`; the member is `Name`.)
- `Mail.DeliverAsync` (`Application/Surl.Api/SurlServerMail.cs:23-35`) resolves its recipient with
  `MailboxStore.LookUpRecipient` (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs:323-345`), the same
  call SMTP's `RCPT TO` makes (`SmtpSession.cs:275`): under `AllowAnonymous` every valid recipient goes
  to the one anonymous mailbox set; otherwise, with no `MailDomains`, only the local part counts, the domain is
  ignored, and an account is matched ordinally or else as the one account equal ignoring case
  (`LocalPartLookup.cs`, ADR-0050 decision 5); with a mail domain the routing of ADR-0102 decision 1.1 applies. So `ALICE@example.com` and `alice@other.org` both reach account `alice`.
- Stand-in: documentation for test authors seeding and observing an instance; no behaviour changes.

## Acceptance criteria

- [x] `README.md`'s "Configuring a real server" example and its prose agree on who can read share `docs` over HTTP,
      consistent with ADR-0102 decision 1.4; if the config file changes, the "was run with `surl -v -K surl.cfg`"
      sentence gives the date it was run again.
- [x] `Application/Surl.Api/README.md` says flags are IMAP system flags with their backslash, e.g. `\Seen`.
- [x] `Application/Surl.Api/README.md` says `StoreKind`, `StoreChangedEvent` and `SurlEvent` are in
      `Surl.Protocol.Abstractions`, and the `using` comment in the first example names them.
- [x] The "Finding and waiting for what happened" section shows reading the path from the awaited event, e.g.
      `((StoreChangedEvent)delivered).Name` or a `is StoreChangedEvent changed` pattern.
- [x] The seeding section states `Mail.DeliverAsync`'s recipient rule as above, with and without a mail domain.

## Notes

- Config changed (`protocols=smtp,imap,pop3,http` on the mailbox, ADR-0102 decision 1.4) but the file was not re-run: the build output folder was unreadable in this unattended lane, so the README says so plainly instead of dating a new run.
- `align-and-document` only: no code changes. `surl-mcp`'s `seed_mail` text is BL-1386; the `RecordExchanges`
  message is BL-1383.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
- 2026-11-18: Doing -> Done. README share example and Surl.Api README corrected
