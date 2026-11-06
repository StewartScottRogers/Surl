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
completed: 2026-11-06
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

- [x] Named tests: each tool's success result has the ADR's shape; `seed_mail` then `get_exchanges` of a fake POP3
      fetch shows the seeded bytes; `reset_server` empties it; `snapshot_server` then `restore_server` round-trips;
      `add_fault` then a fake request shows the fault and `remove_fault` ends it; past the snapshot bound, an
      unknown server ID or snapshot ID, and a bad fault item are each `isError: true` with the ADR's text; no result
      or log line holds a password. (Nine tools; `list_faults` split to BL-955 and BL-956, see Notes.)
- [x] A named test shows `tools/list` lists every new tool with a JSON object input schema and a description.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` and `Surl.Mcp`
      keep 100% line and branch coverage.

## Notes

- **Split: `list_faults` moved to BL-955 and BL-956.** Its `remaining` needs the fault plan's live counts, which
  only `Surl.Core`'s `FaultPlan.Rules` holds, kept private by `Surl.Api`'s `SurlServerFaults`. `Surl.Api` was in
  BL-811's `touches` (in Doing), so rather than send the whole task back to Backlog for one listing method, the
  nine other tools landed here and BL-955 (a `SurlServerFaults` listing in `Surl.Api`) and BL-956 (`list_faults`
  in `Surl.Mcp`) were filed. Counting `FaultAppliedEvent`s in the host was rejected: the event record is bounded,
  so `remaining` would be overstated once events drop.
- Decisions (Claude, under Stewart's delegation; ADR-0124 decision 8 leaves the texts open, and they are recorded
  in `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`, inside this task's `touches`):
  - Snapshots are kept per server in `ServerSnapshots` (`p1`, `p2` ... per server, never reused, newest 4 kept);
    restoring one not kept is `server <id> has no snapshot <p>; only its newest 4 snapshots are kept` - the
    "past the snapshot bound" error.
  - Content is exactly one of the text and the base64 property (`give exactly one of <a> and <b>`), at most
    16 MiB decoded (`<name>: <n> bytes is more than the 16777216 (16 MiB) one seed may carry`).
  - `publish_message`: exactly one of `topic` and `queue`; `retain` with `queue` is refused.
  - `add_account` writes its own refusal texts, so no exception message - and never the password - is echoed.
  - `add_fault` reads the item with `surl`'s own `CommandLineParser`, so a bad item answers `option --fault: ...`,
    then turns the `ConfiguredFault` into the `SurlFault` `Surl.Api` takes; a 257th rule answers the plan's
    `A fault plan holds at most 256 rules.`
  - Every `InvalidOperationException` from `Surl.Api` (seed refusals, disk-store refusals, snapshot size) is
    `isError` with its message, in one place, `RunningServerTool`.
- Tests: `StoreToolsTests` and `FaultToolsTests` (one class per tool family, since each test drives several tools
  against one fake client) and `ProgramTests`' `tools/list` tests. The disk-store refusal test touches the file
  system, so it is `Integration` (passes); the fast run covers the same catch through seed refusals and the 257th
  rule. `Measure-CodeQuality.ps1 -Library Surl.Mcp.UnitLibrary`: 100% line, 100% branch, worst CRAP 10. `Surl.Mcp`
  (assembly `surl-mcp`) is outside that script's list; its new lines are the registrations `ProgramTests` runs.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. surl-mcp seeds, resets, snapshots, restores and adds and removes faults on a running server through nine tools; list_faults split to BL-955/BL-956
