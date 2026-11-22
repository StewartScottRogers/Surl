---
id: BL-1593
title: Let surl-mcp mail tools name the anonymous mailbox owner
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1593 — Let surl-mcp mail tools name the anonymous mailbox owner

## Goal

`surl-mcp`'s `list_mailboxes`, `list_messages` and `read_message` accept `"owner": ""`, the owner of the one mailbox
set `--allow-anonymous` makes, so an agent can read mail delivered to an anonymous SMTP/POP3/IMAP server.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T0726-Surl-bug-report.md` item 5: `start_server`
  with `--allow-anonymous` and SMTP+POP3, send mail, then `list_messages` with `{"owner": ""}` is refused as invalid
  params.
- Cause: each tool's input schema declares `"owner": { "type": "string", "minLength": 1, ... }`:
  `Application/Surl.Mcp.UnitLibrary/ListMailboxesTool.cs:28`, `ListMessagesTool.cs:29`, `ReadMessageTool.cs:28`.
- Below the tool nothing refuses it: `Application/Surl.Api/SurlServerMail.cs` only refuses `null`, and
  `Surl.Composition`'s `ComposedStores.ListMailboxes`/`ListMessages` go through `MailboxStore.ViewFor`
  (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs:387-392`), which under `--allow-anonymous` gives the
  anonymous owner's view for any name; the anonymous owner's name is empty (`MailStoreChangeName.cs:9`), so `""` is
  the name events and `get_events` show for it.
- Stand-in: no store, event or fault change; this lets an MCP agent observe the shared mail store of an anonymous
  instance, as `Surl.Api` already can.

## Acceptance criteria

- [ ] The three schemas drop `minLength` for `owner` (keep it required) and each description says the empty string names
      the `--allow-anonymous` mailbox owner.
- [ ] A new fast test in `Application/Surl.Mcp.UnitTests/ReadBackToolsTests.cs` starts a server with `--allow-anonymous`,
      seeds one message, and reads it with `list_mailboxes`, `list_messages` and `read_message` using `"owner": ""`.
- [ ] A test shows `"owner": ""` on a server without `--allow-anonymous` and no empty-name owner answers the tool's
      existing not-found result, not invalid params.
- [ ] Any test that pins the tools' input schemas is updated to the new schema.
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` clean, fast tests green, `Surl.Mcp.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
