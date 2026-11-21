---
id: BL-1514
title: Show a mailbox's use in the surl-mcp mailbox tool
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1512]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1514 — Show a mailbox's use in the surl-mcp mailbox tool

## Goal

The `surl-mcp` mailbox listing tool shows a mailbox's `use` so an AI agent can see which folder is Sent.

## Context

ADR-0055 Amendment 1, decision 6. Depends on `SurlMailboxInfo.Use` in Surl.Api (BL-1512). Code: `Application/Surl.Mcp.UnitLibrary/ListMailboxesTool.cs`, result `{"mailboxes": [{"name", ...}]}`, read through `SurlServer.Mail.ListMailboxesAsync`. Add `use` only when a mailbox has one; keep the tool's description and any output schema true of the new field.

## Acceptance criteria

- [x] The tool result includes `"use"` for a mailbox with a use and omits it otherwise, so existing results are byte-identical.
- [x] The tool's description and output schema (if any) name the `use` field.
- [x] Tests exist and pass, coverage stays 100%, and `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

- `use` is written only when non-null; description and doc comment name it. The tool has no output schema.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. list_mailboxes shows a mailbox's use when it has one
