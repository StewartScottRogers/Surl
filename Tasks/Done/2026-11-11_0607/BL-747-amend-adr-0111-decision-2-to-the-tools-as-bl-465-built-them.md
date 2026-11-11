---
id: BL-747
title: Amend ADR-0111 decision 2 to the tools as BL-465 built them
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-465]
touches: [Documentation/Planning/Decisions/ADR-0111-how-the-mcp-server-answers-mcp-mcps-and-mcp-stdio.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-747 — Amend ADR-0111 decision 2 to the tools as BL-465 built them

## Goal

ADR-0111 says what `Surl.Extended.Protocol.McpServer`'s tools do as BL-465 built them, so no statement in it is
false of the code.

## Context

BL-465 could not edit `Documentation/Planning/Decisions` (BL-441 held it), so its departures from ADR-0111 are
recorded in BL-465's Notes. Amend the ADR (an "Amended" line in its header, the text changed in place):

- Decision 1, "One registry per server": the server builds one registry **per session**
  (`McpServerTools.CreateRegistry(store, caller, maximumMessageBytes)`), because a tool's handler receives only
  its arguments and token, and every file operation runs as the session's caller.
- Decision 2's table: a dot-file (without `--serve-dot-files`) and `.surl` are answered
  `no such file or folder`, because `Surl.Content` answers them as absent; `the path is refused` is every
  `ContentPathRefusal` (`..`, a drive or colon, a separator in a segment, `//`, a device name, a link out of the
  root). A write to a dot-file, a read-only share or a missing folder is `writing is not permitted`.
- Decision 2, added texts: `read_file` on a folder is `the path is a folder, not a file`; `list_directory` on a
  file is `the path is a file, not a folder`; `write_file` whose `base64` does not decode is
  `base64 is not valid base64`.
- Decision 2, `write_file` and `--max-filesize`: checked by `ContentStore.WriteUploadAsync`, which writes to a
  temporary file and deletes it, so the target is never touched (not checked before the store is called).
- Decision 2, `read_file` and the message limit: refused when the file's length, the JSON-escaped text, or the
  base64 passes `--max-message`.
- Decision 2, `write_file`'s `structuredContent.path` and its text name the path without its leading `/`.
- Each tool has a `title`: `List a folder`, `Read a file`, `Write a file`, `Echo`.

## Acceptance criteria

- [x] ADR-0111 decisions 1 and 2 state each point above, and its header carries an "Amended 2026-11-05 by
      BL-745" line.
- [x] `Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary/CLAUDE.md` and the ADR agree.

## Notes

The header line reads "Amended ... (2026-11-06, BL-747 ...)": the criterion's "2026-11-05 by BL-745" named the
filing task and date, not the task and day that made the change.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0111 decisions 1 and 2 say what the MCP server's tools do as BL-465 built them, each point checked in the code
