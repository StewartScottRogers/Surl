---
id: BL-677
title: Serve shared folders and mailbox quotas over IMAP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-675]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-677 — Serve shared folders and mailbox quotas over IMAP

## Goal

The IMAP server lists, selects and fetches from shared folders for their members and answers an append or copy
past a mailbox quota with the reply BL-642's ADR measured, without changing anything for a `surl` with no shared
folders or quotas.

## Context

- Decision: BL-642's ADR decision 1 (how IMAP shows a shared folder - `NAMESPACE` per RFC 2342 or plain `LIST`
  names - and the quota reply), measured against pinned upstream curl 8.21.0 (`curl imap://…/` listing,
  `curl imap://…/<shared>;UID=1`, `curl -T m imap://…/<mailbox>` over quota, and curl's exit codes). ADR-0055 is
  how the IMAP server answers today.
- Where: `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary`, calling BL-675's store features.
- `UpstreamCurlReadsMailFromSurlOverImapTests` must pass unchanged.

## Acceptance criteria

- [x] Named tests replay sessions where: a member's `LIST` shows the shared folder as the ADR says and a non-member's
      does not; a member selects and fetches from it; an `APPEND` past quota is answered with the ADR's reply and
      nothing is stored; a `COPY` past quota likewise.
- [x] Every existing test in `Protocols/Mail/Surl.Protocol.Imap.UnitTests` passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Behaviour is ADR-0102 decisions 1.4 and 1.6 as written (rows R6 to R11); no new ADR. `ImapResponses` maps
  `QuotaExceeded` to `NO [OVERQUOTA] Mailbox is over its quota` (APPEND, COPY, MOVE) and `SharedFolderProtected`
  to `NO [NOPERM] A shared folder cannot be deleted` / `renamed` / `created under` by command. `NAMESPACE` names
  `(("Shared/" "/"))` only for a view with shared folders. APPEND checks `CheckQuotaForAppend` after
  `--max-filesize` (TOOBIG first: a message past the server's limit is refused for that, whatever the quota) and
  before `+`, and notes the refusal. LIST needed no change: the store lists `Shared/<name>` and `ImapMailboxList`
  already adds the `\Noselect \HasChildren` parent, matching R7.
- Touches widened to `Surl.MailStore.UnitLibrary`/`.UnitTests` (no Doing task names them): `ReadMailbox` gave a
  shared folder's snapshot its stored name (`team`), so a selected `Shared/team` was re-read as `team`, missing, and
  every message reported expunged after SELECT; STATUS printed `team`. The snapshot is now named as the view names
  it (`ReadMailbox_SharedFolder_NamesTheSnapshotAsTheViewNamesIt`).
- Tests: `ImapSharedFolderAndQuotaTests` (LIST member/non-member, NAMESPACE, SELECT + UID FETCH by a member,
  STATUS, non-member missing-mailbox answers, NOPERM for DELETE/RENAME/CREATE, APPEND over/within quota, COPY and
  UID COPY over quota, COPY into a shared folder counting against no quota). IMAP 100% line and branch; MailStore
  100% line and branch. Measure-CodeQuality flags Cobertura complexity 12 on `MailDomainConfiguration..ctor` and
  `MailboxStore.DeliverPending`, both from BL-648/BL-675 and untouched here; the build's CA1502 gate passes them.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. IMAP lists, selects and fetches shared folders for members and answers APPEND/COPY past a quota with NO [OVERQUOTA]
