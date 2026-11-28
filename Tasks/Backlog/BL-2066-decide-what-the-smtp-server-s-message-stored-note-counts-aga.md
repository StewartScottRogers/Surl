---
id: BL-2066
title: Decide what the SMTP server's Message stored note counts, against ADR-0053 decision 8
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2066 — Decide what the SMTP server's Message stored note counts, against ADR-0053 decision 8

## Goal

A dated amendment to ADR-0053 settles what `<k>` counts in `Message stored: <n> bytes for <k> recipients`, and
whether the note is written when every recipient is discarded, saying whether the ADR or the code is changed.

## Context

Found while working BL-2049 (checked by reading the code and tests on 2026-11-28, not by running them).

- `Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md`, decision 8's table, row
  "A message stored": `<k>` is "the owners it reached".
- `SmtpSession.DeliverAsync` (`Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs`, line 372) writes
  `accepted.Deliverable.Count` for `<k>`, after every `DeliverAsync` that succeeds. `SmtpMailTransaction.Deliverable`
  counts recipient entries: an account named twice once, an alias once however many owners it names, an alias whose
  owners were all named already not at all, and the anonymous owner once per recipient.

With `team@` naming Ann and Bob and `pair@` naming Bob and Cat (the aliases of `SmtpMailDomainTests`):

| Accepted `RCPT`s | `<k>` today | Owners reached |
| --- | --- | --- |
| `team@` | 1 | 2 |
| `team@`, then `pair@` | 2 | 3 |
| two, under `--allow-anonymous` | 2 | 1 (the anonymous owner, two copies) |
| only addresses that name no account | 0, and nothing is stored | 0 |

In the last case the log holds `Mail for <address> discarded: no such account` and then
`Message stored: <n> bytes for 0 recipients`.

Tests that pin the text, in `Protocols/Mail/Surl.Protocol.Smtp.UnitTests/`: `SmtpDeliveryTests.cs` line 38
(`1 recipients`) and line 238 (`0 recipients`, in `ServeAsync_NoRecipientNamesAnAccount_StoresNothingAndAnswers250`),
and `SmtpMailDomainTests.cs` line 92 (`1 recipients` where Ann and Bob each get a copy).

Earlier records, neither of which is an ADR:

- BL-767 (Done) Notes: `KeepNamedRecipient` was chosen over a merge so the note kept its count for the existing tests.
- BL-2000 (Done) Notes: "Default taken: the `Message stored: ... for N recipients` note still counts `Deliverable`
  elements."
- BL-1995 (Deferred, a duplicate of BL-2000) lists the note, counting `Deliverable` elements, among the things it
  leaves unchanged. This task does not revive BL-1995 or take over the further tests it names. If the decision changes
  the count, the amendment supersedes that line of BL-1995; leave BL-1995's file as it is.

The decision is Claude's (root `CLAUDE.md`, "Decisions"). The note is written to the verbose log only and no wire byte
depends on it, so no upstream curl measurement is needed. Stand-in: the shared mail store; no new event, seed, reset
or fault, since each delivery's `StoreChangedEvent` already tells a test which owner was reached.

## Acceptance criteria

- [ ] ADR-0053 has a new dated amendment, marked "Decided by Claude under Stewart's delegation" and naming BL-2066, listed in the ADR's header and pointed to from decision 8's "A message stored" row.
- [ ] The amendment states what `<k>` counts, giving its value for each row of the table in Context and for one account named by two accepted `RCPT`s.
- [ ] The amendment states whether a note is written when every recipient is discarded and, if one is, its exact text.
- [ ] Every sentence in the amendment and the marked row about what the server does today is true of `SmtpSession.DeliverAsync`; wherever the decision differs from the code, it is written as intent and names the task that will change the code.
- [ ] If the decision differs from the code, a `Backlog` task filed with `task-board.ps1 new` touches only `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary` and its `.UnitTests` project, and names each test whose expected note changes; if it does not differ, this task's Notes say no code task is needed.
- [ ] This task changes no file under `Protocols/`.

## Notes

## Log

- 2026-11-28: Created.
