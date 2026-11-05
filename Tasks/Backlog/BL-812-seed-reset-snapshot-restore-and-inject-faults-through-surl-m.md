---
id: BL-812
title: Seed, reset, snapshot, restore and inject faults through Surl.Mcp's tools
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-810, BL-809]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-812 — Seed, reset, snapshot, restore and inject faults through Surl.Mcp's tools

## Goal

An agent driving `surl-mcp` can seed a running server (a message into a mailbox, a file, a message on a topic or
queue, an account), reset it, snapshot and restore it, and add, list and remove fault rules, through tools that
call the same `Surl.Api` surface a .NET test uses, under `surl-mcp`'s existing safety rules.

## Context

- Decision: BL-822's ADR decision 8 (amends ADR-0103 decision 3: tool names - planning `seed_mail`, `put_file`,
  `publish_message`, `add_account`, `reset_server`, `snapshot_server`, `restore_server`, `add_fault`,
  `list_faults`, `remove_fault` - schemas, results and bounds, including how many snapshots the host keeps per
  server and that a snapshot is referred to by an ID, never returned whole).
- Built on BL-810 (`Surl.Api` store control) and BL-808 (faults). ADR-0103 decision 4's safety rules apply: no
  local path is read outside `--root`; content arrives as UTF-8 text or base64 as `start_server`'s `files` and
  `files_base64` do; an account's password is never echoed in a result or log line.
- Where: tool logic in `Application/Surl.Mcp.UnitLibrary`; one registration line per tool in
  `Application/Surl.Mcp`. `instructions` and `--help` mention the new tools.
- Stand-in: controllable and fault-injectable, for agents.

## Acceptance criteria

- [ ] Named tests: each tool's success result has the ADR's shape; `seed_mail` then `get_exchanges` of a fake POP3
      fetch shows the seeded bytes; `reset_server` empties it; `snapshot_server` then `restore_server` round-trips;
      `add_fault` then a fake request shows the fault and `remove_fault` ends it; past the snapshot bound, an
      unknown server ID or snapshot ID, and a bad fault item are each `isError: true` with the ADR's text; no result
      or log line holds a password.
- [ ] A named test shows `tools/list` lists every new tool with a JSON object input schema and a description.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` and `Surl.Mcp`
      keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
