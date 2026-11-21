---
id: BL-1485
title: Measure what pinned upstream curl 8.21.0 sends and prints against an IMAP server that advertises SPECIAL-USE
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitTests/Fixtures]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1485 — Measure what pinned upstream curl 8.21.0 sends and prints against an IMAP server that advertises SPECIAL-USE

## Goal

Recordings under `Protocols/Mail/Surl.Protocol.Imap.UnitTests/Fixtures` show what pinned upstream curl 8.21.0 sends,
prints and exits with against an IMAP server that advertises `SPECIAL-USE` (RFC 6154) and marks `Sent`, `Drafts` and `Trash`
with `\Sent`, `\Drafts`, `\Trash`, so BL-1486 decides from measurement whether and how Surl offers SPECIAL-USE.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 9 (an ask): no
  SPECIAL-USE attributes on start folders (`--mailbox ...;folders=Sent,Drafts`). Other people's mail clients find "Sent" by
  `\Sent`, so it may make a fuller stand-in; whether upstream curl reads it is not known.
- Today: `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapCapabilities.cs` advertises no `SPECIAL-USE`, and
  `ImapMailboxList.cs` writes `* LIST` lines without use attributes. No upstream curl 8.21.0 test case mentions SPECIAL-USE,
  XLIST or `\Sent` (`Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data`, tag `curl-8_21_0`, searched 2026-11-20).
- Measure with `Record-CurlExchange.ps1 -Imap` and `-ImapReply` overrides (its help, `.PARAMETER Imap` and `.PARAMETER ImapReply`),
  only with the win-x64 build pinned in `UpstreamCurlBuilds.json`. Never the Curl port (ADR-0003). Follow the folder layout and
  README of the existing fixtures (`Fixtures/README.md`).

## Acceptance criteria

- [x] Recorded cases, each a fixture folder with the recorder's five files, the greeting and `CAPABILITY` advertising `SPECIAL-USE`:
      `curl -u u:p imap://127.0.0.1:<port>/` (a plain listing, `LIST` answered with `\Sent`, `\Drafts`, `\Trash` attributes);
      `-X 'LIST "" "*" RETURN (SPECIAL-USE)'`; `-X 'LIST (SPECIAL-USE) "" "*"'`; and `-T mail.txt imap://.../Sent` (an upload).
- [x] Each folder's transcript shows whether curl itself ever sent `RETURN (SPECIAL-USE)` or a selection option unasked, and what
      it printed of the attributes on stdout.
- [x] `Fixtures/README.md` lists each case, the command line, the curl build's SHA-256 and the date measured, and states in one
      sentence whether upstream curl 8.21.0 uses SPECIAL-USE on its own.
- [x] No test needs `TestCategory=Integration`, and the fast tests stay green (the new folders are not replayed by
      `RecordedFixtureTests` until BL-1486's follow-up says so; exclude them there if it would pick them up).

## Notes

- Recorded 2026-11-20 into four `special-use-*` folders; curl exited 0 on all, never sent RETURN or a selection option unasked, printed attributes on stdout unchanged. Replay tests name cases explicitly, so the new folders are not picked up.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Recorded pinned curl 8.21.0 against SPECIAL-USE IMAP: it never uses SPECIAL-USE unasked
