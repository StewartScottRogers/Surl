---
id: BL-202
title: Answer IMAP FETCH, UID FETCH and SEARCH in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-201]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests]
requirement: FR-044
created: 2026-09-29
completed:
---
# BL-202 — Answer IMAP FETCH, UID FETCH and SEARCH in Surl.Protocol.Imap

## Goal

`ImapProtocolServer` answers `FETCH`, `UID FETCH`, `SEARCH` and `UID SEARCH` with the data items,
sections, partials and search keys BL-187's ADR supports, so every `imap://` URL form curl
fetches with (`;UID=`, `;MAILINDEX=`, `;SECTION=`, `;PARTIAL=`, `?<search>`) works.

## Context

- Decisions: BL-187's ADR (item formats, literals, the search keys, `\Seen` on a non-peek fetch,
  `UIDVALIDITY` mismatch handling); RFC 3501 sections 6.4.4 (`SEARCH`), 6.4.5 (`FETCH`), 6.4.8
  (`UID`), 7.4.2 (the `FETCH` response).
- Message parts (header, text, `HEADER.FIELDS`, MIME part numbers) are computed from the stored
  bytes by RFC 5322 and RFC 2045 structure as far as the ADR requires; keep the parser in this
  library unless the ADR placed it in `Surl.MailStore`.
- Fixtures: BL-187's recordings of each URL form.

## Acceptance criteria

- [ ] A fast test replays each fetch and search fixture and asserts surl's responses (literal
      sizes included) are the ADR's, byte for byte.
- [ ] Fast tests cover: a message number or UID out of range; a sequence set with ranges and `*`;
      a section of a message with no such part; `BODY.PEEK` leaving `\Seen` unset and `BODY`
      setting it; each supported search key once, and an unsupported one answered as the ADR says.
- [ ] `dotnet build Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
