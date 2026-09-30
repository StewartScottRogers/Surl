---
id: BL-203
title: Answer IMAP APPEND and the mailbox-changing commands in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-202]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests]
requirement: FR-044
created: 2026-09-29
completed:
---
# BL-203 — Answer IMAP APPEND and the mailbox-changing commands in Surl.Protocol.Imap

## Goal

`ImapProtocolServer` answers `APPEND` (curl's `-T`) and the commands that change mailboxes and
messages, which curl sends through `-X` - `CREATE`, `DELETE`, `RENAME`, `SUBSCRIBE`,
`UNSUBSCRIBE`, `STORE`, `COPY`, `EXPUNGE` and their `UID` forms, and any other BL-187's ADR
lists - against the mail store.

## Context

- Decisions: BL-187's ADR (responses, flags, `APPEND`'s literal bound, whether `--allow-uploads`
  gates it per BL-184's ADR); ADR-0006 section 5 (an `APPEND` past `--max-filesize` answered
  tagged `NO`, nothing stored); RFC 3501 sections 6.3.3 to 6.3.11, 6.4.3, 6.4.6, 6.4.7.
- Fixtures: BL-187's recordings of `-T` and the `-X` commands.

## Acceptance criteria

- [ ] A fast test replays each fixture named in Context and asserts surl's responses and the
      store's contents afterwards.
- [ ] Fast tests cover: `APPEND` to a missing mailbox (`[TRYCREATE]` if the ADR says so); an
      `APPEND` literal past `--max-filesize`; `DELETE` and `RENAME` of `INBOX`; `EXPUNGE`'s
      untagged responses in order; `STORE` with `+FLAGS`, `-FLAGS` and `.SILENT`; `COPY` to a
      missing mailbox; a write refused where BL-184's ADR gates it.
- [ ] `dotnet build Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
