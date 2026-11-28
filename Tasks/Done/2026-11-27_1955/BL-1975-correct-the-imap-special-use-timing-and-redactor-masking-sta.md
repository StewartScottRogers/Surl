---
id: BL-1975
title: Correct the IMAP SPECIAL-USE timing and redactor masking statements to match the code
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md, Protocols/Mail/Surl.Protocol.Imap.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1975 — Correct the IMAP SPECIAL-USE timing and redactor masking statements to match the code

## Goal

ADR-0055 and the XML comment on `ImapProtocolServer.CreateRedactor` say truthfully when `SPECIAL-USE` is offered and
what the exchange log masks.

## Context

Found while working BL-1956 (2026-11-27), read from source, not run. Documentation only: an ADR amendment and comments.

**1. When `SPECIAL-USE` is offered.** ADR-0055, Amendment 1, point 3 says a store *started* with at least one use
advertises `SPECIAL-USE CREATE-SPECIAL-USE`, and "It is decided at start, not per command, so a test sees one stable
`CAPABILITY`." The code asks `MailboxStore.HoldsAnyFolderUse` each time it writes the capability list
(`Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapSession.cs`, about lines 115-116). BL-1510's Notes record that
deleting the last use therefore withdraws the capability. `CREATE` asks the same property
(`ImapSession.Changes.cs`, about line 135), so after the last mailbox with a use is deleted over IMAP,
`CREATE Sent (USE (\Sent))` is `NO [USEATTR]` and the client cannot make it again that way.

Decide which is right (root `CLAUDE.md`, "Decisions" and "Surl is a stand-in for tests") and record it as a new
amendment in `Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md`. To weigh:
keeping the code needs only the amendment; deciding at start gives point 3's stable `CAPABILITY` and lets a client
under test delete and recreate its Sent folder. Upstream curl 8.21.0 uses neither (ADR-0055, Amendment 1,
"Measured"), and the Curl port is not evidence. If the code is judged wrong, change no behaviour here: write the
decision as intent and have `task-planner` file the code task.

**2. The redactor comment.** The XML comment on `CreateRedactor`
(`Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapProtocolServer.cs`, about lines 117-123) says the redactor writes
the `LOGIN` password and every `AUTHENTICATE` secret as `****`, "leaving literals such as an `APPEND` message
unchanged". Two facts are missing, and without them the sentence misleads:

- `ImapLineRedactionRule` also masks a `LOGIN` password sent as a literal; only other literals are logged unchanged.
- `LineBytesRedactor` (`Foundation/Surl.Protocol.Abstractions.UnitLibrary`, `DefaultMaxHeldBytes`, 64 KiB) writes a
  run of more than 64 KiB with no CR LF as `****` whatever it holds, an `APPEND` message included, and drops the rest
  of that line up to its line end (ADR-0182, decision 6).

The IMAP library's `CLAUDE.md` describes both behaviours as the code has them (BL-1956). No store, event or fault is
involved: no behaviour changes.

## Acceptance criteria

- [x] ADR-0055 has a new dated amendment, marked decided by Claude under Stewart's delegation, that says whether `SPECIAL-USE` is evaluated each time the capability list is written or fixed at start, why, and what a client sees after the last mailbox with a use is deleted, `CREATE` with `USE` included.
- [x] No reader can take Amendment 1, point 3's "decided at start, not per command" for current behaviour when it is not: either that sentence is marked superseded by the new amendment, or the new amendment says the code asks per command today, that deciding at start is intent, and names the filed code task.
- [x] The `CreateRedactor` XML comment says a `LOGIN` password sent as a literal is masked and that other literals, an `APPEND` message included, are logged unchanged apart from the 64 KiB case.
- [x] The same comment says a run of more than 64 KiB with no CR LF is written as `****` whatever it holds.
- [x] Only comments change in `.cs` files; `dotnet build Protocols/Mail/Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean and the `Surl.Protocol.Imap.UnitTests` fast tests pass.

## Notes

- Decision (ADR-0055 Amendment 2): `SPECIAL-USE` is fixed at start, so Amendment 1 point 3 stands and the code is
  judged wrong. A real server does not withdraw an extension when a folder is deleted, and a client under test must be
  able to delete and recreate its Sent folder. No behaviour changed here; BL-2013 builds it. Point 3 also carries a
  one-line pointer saying the sentence is intent.
- "At start" is defined as when the `ImapProtocolServer` is constructed, so BL-2013 needs no new store member.
- The edits were made in the session rather than by the `align-and-document` agent: one amendment and one comment.
- Not filed: ADR-0183 amendment 1 point 5 already records, as a known limit, that after a run past 64 KiB inside a
  literal the redaction rule's literal count runs long (only the log is affected).
- Gates: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 30417 passed,
  0 failed, 3 skipped (`Surl.Protocol.Imap.UnitTests` 841 passed).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0055 Amendment 2 decides SPECIAL-USE is fixed at start (code follows in BL-2013) and the CreateRedactor comment states what the IMAP exchange log masks
