---
id: BL-271
title: Stream SMTP DATA into a pending mail-store file and answer a failed store write with 451 4.3.0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests]
requirement: FR-043
created: 2026-09-30
completed:
---
# BL-271 — Stream SMTP DATA into a pending mail-store file and answer a failed store write with 451 4.3.0

## Goal

`SmtpSession` streams each `DATA` body, trace fields first, into a `PendingMessage` from
`MailboxStore.CreatePendingMessage` and delivers it with
`MailboxStore.Deliver(IReadOnlyList<MailRecipient>, PendingMessage)`, and answers a message the
store could not write with `451 4.3.0 Local error in processing`, as ADR-0050 decision 7 and
ADR-0053 decision 6 say.

## Context

- Found by BL-214 (documenting Phase 3 mail, 2026-09-30): the SMTP server diverges from its ADRs.
- Today: `Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs` `ReceiveMessageAsync` reads the body into
  `SmtpMessageBodyBuffer` (a `MemoryStream` with a budget) and `DeliverAsync` hands
  `[.. traceFields, .. body.ToArray()]` to `MailboxStore.Deliver(..., ReadOnlySpan<byte>)`, the
  overload `Surl.MailStore.UnitLibrary/CLAUDE.md` says is kept only "for the servers not yet
  streaming" (it holds the bytes in memory until the next save). `SaveMailStoreAsync` notes a
  failed save and still answers `250`. `SmtpReplies` has no `451` reply.
- `Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md`
  decision 7: a message file is written once through `messages/.pending-<guid>`, "the server
  streams the body into it as it reads", then renamed; an *index* write that throws leaves the
  change in memory and the next save writes it.
- `Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md`
  decision 6: the budget is `MaxUploadBytes` minus the trace fields' length (none when
  `MaxUploadBytes` is 0); past it `552 5.3.4 Message exceeds the size limit`, nothing stored (the
  pending file is deleted), connection closed; store outcomes `StoreFull` -> `452 4.3.1
  Insufficient system storage` and `StorageFailed` -> `451 4.3.0 Local error in processing`, the
  exception message in a note (`PendingMessage.StorageFailure`), never in the reply, session goes
  on; a peer that closes mid-body stores nothing. Decision 10's row 13 is the measurement: upstream
  curl 8.21.0 exits 8 for `451 4.3.0` after the body.
- The store side exists (BL-227): `MailboxStore.CreatePendingMessage`, `PendingMessage.Body`
  (never throws for a failing file, so the rest of the body can still be read off the wire),
  `Deliver(..., PendingMessage)` renaming or deleting the file and returning `StorageFailed`.
  IMAP's `APPEND` already streams this way (`Surl.Protocol.Imap.UnitLibrary`); use it as the model.
- The budget check that `SmtpMessageBodyBuffer` does today (count every byte, keep none past the
  budget, `IsPastBudget`) still has to happen: wrap or replace it so it counts while writing to
  `PendingMessage.Body`, and dispose the pending message (deleting its file) on every path that
  does not deliver it. Remove `SmtpMessageBodyBuffer` if nothing uses it afterwards.

## Acceptance criteria

- [ ] `SmtpSession` no longer calls `MailboxStore.Deliver` with a byte array or span; it writes the
      trace fields and the body into a `PendingMessage` and delivers that.
- [ ] `SmtpReplies` has a `451 4.3.0 Local error in processing` reply, and a delivery answered
      `StorageFailed` gets it, notes `Mail store: <exception message>`, stores nothing, and the
      session goes on (a following `MAIL` is answered).
- [ ] In `Surl.Protocol.Smtp.UnitTests/SmtpDeliveryTests.cs`,
      `ServeAsync_StoreThatCannotBeWritten_AcceptsTheMessageAndNotesTheFailure` is replaced by a test
      (named for what it shows, e.g. `ServeAsync_MessageFileCannotBeWritten_Answers451AndTheSessionGoesOn`)
      pinning the `451` reply, the note and an empty `INBOX`; and a test pins that an index save that
      throws after a delivered message still answers `250` and notes the failure (ADR-0050 decision 7).
- [ ] Tests pin that a body past the budget and a peer closing mid-body leave no `.pending-` file
      in the store's file system, and that a stored message's bytes are the trace fields followed by
      the body exactly as before (the existing delivery tests pass unchanged).
- [ ] The recorded fixture rows in `Surl.Protocol.Smtp.UnitTests/RecordedFixtureTests.cs` still pass.
- [ ] `dotnet build Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean; the fast tests pass; no
      test needs `TestCategory=Integration`; `Surl.Protocol.Smtp.UnitLibrary` keeps 100% line and
      branch coverage.
- [ ] `SmtpProtocolServer`'s and `SmtpSession`'s doc comments and
      `Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md` say the body streams into a pending file and name
      the `451` reply.

## Notes

- Touches only the SMTP library and its tests. Once SMTP streams, no server calls
  `MailboxStore.Deliver(..., ReadOnlySpan<byte>)`; retiring that overload and its CLAUDE.md
  paragraph in `Surl.MailStore.UnitLibrary` is a separate follow-up task to file, not part of this one.
- If implementing shows a reason to keep buffering, amend ADR-0050 decision 7 and ADR-0053
  decision 6 instead (a `docs` follow-up), rather than leaving code and ADR apart.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-214 (Phase 3 mail documentation found SMTP diverging from ADR-0050 decision 7 and ADR-0053 decision 6).
- 2026-09-30: Backlog -> Doing.
