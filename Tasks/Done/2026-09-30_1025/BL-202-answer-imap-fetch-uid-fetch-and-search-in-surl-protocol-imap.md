---
id: BL-202
title: Answer IMAP FETCH, UID FETCH and SEARCH in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-201]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests, Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md]
requirement: FR-044
created: 2026-09-29
completed: 2026-09-30
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

- [x] A fast test replays each fetch and search fixture and asserts surl's responses (literal
      sizes included) are the ADR's, byte for byte.
- [x] Fast tests cover: a message number or UID out of range; a sequence set with ranges and `*`;
      a section of a message with no such part; `BODY.PEEK` leaving `\Seen` unset and `BODY`
      setting it; each supported search key once, and an unsupported one answered as the ADR says.
- [x] `dotnet build Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- **Built:** `FETCH`, `UID FETCH`, `SEARCH` and `UID SEARCH` in `ImapSession`, with a sequence-set
  reader (`ImapSequenceSet`), the fetch-item and section grammar (`ImapFetchRequest`,
  `ImapFetchItem`, `ImapSection`), an RFC 5322 / RFC 2045-2046 reader (`ImapBodyPart`,
  `ImapHeaderField`, `ImapHeaderText`, `ImapMimeValue`, `ImapContentType`, `ImapAddress`), the
  response writers (`ImapFetchResponse`, `ImapStructureWriter`, `ImapDataWriter`) and the search
  keys (`ImapSearchParser`, `ImapSearchCandidate`). Every RFC 3501 section 6.4.5 item and section
  form and every section 6.4.4 key ADR-0055 lists is answered. BCL only; no regex (the SENT* date
  is read by hand, which also keeps the source-generated regex out of the coverage gate).
- **Fixtures:** BL-187 left no fetch or search recordings in the repository, so eleven were
  recorded here with `Record-CurlExchange.ps1 -Imap` against pinned curl 8.21.0 (win-x64), each fed
  the exact bytes Surl sends, each exit 0: the seven URL forms (`;UID=`, `;MAILINDEX=`,
  `;SECTION=TEXT`, `;SECTION=HEADER.FIELDS (SUBJECT)`, `;SECTION=1`, `;PARTIAL=`, `;SECTION=TEXT;PARTIAL=`),
  `?SUBJECT 1`, and `-X` for `FETCH 1:* ALL`, `UID FETCH ... BODYSTRUCTURE BODY.PEEK[HEADER]` and
  `UID SEARCH UNSEEN`. `RecordedFixtureTests` replays each byte for byte. Recorded on port 18243
  so as not to meet another lane's recorder on 18143; the port is in no byte curl sends.
- **Decisions** (ADR-0055 decision 16, added here; ADR-0055 was added to `touches` for it, as no
  task in `Doing` names it): strings with any byte outside printable ASCII go out as literals; the
  header/MIME reading rules and the 32-level nesting bound; capitals for type, subtype and
  parameter names; `""` as the host of an address with no `@`; a message another session expunged
  answers empty with the 1970 epoch as `INTERNALDATE` and matches no search key; an unreadable
  message file ends the command `NO [SERVERBUG] Could not read the message`; an out-of-range
  number in a `SEARCH` set matches nothing (only `FETCH` answers `BAD`); `UID` + anything but
  `FETCH`/`SEARCH` stays `BAD Command not recognized` until BL-203.
- `FETCH` and `SEARCH` send no pending `EXPUNGE`/`EXISTS` updates before their tagged response
  (ADR-0055 decision 5); `ImapCommandTests` now uses `STORE` as its not-yet-answered example.
- **Measured:** `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`: 100% line,
  100% branch, 0 failing members (400 members, highest CRAP 10). 436 IMAP tests, all over
  `InMemoryConnection` or a wrapper of it; no socket.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ImapProtocolServer answers FETCH, UID FETCH, SEARCH and UID SEARCH; every curl imap:// fetch and search URL form replays byte for byte; 100% line and branch coverage
