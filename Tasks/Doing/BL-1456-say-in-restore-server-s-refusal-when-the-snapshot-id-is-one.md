---
id: BL-1456
title: Say in restore_server's refusal when the snapshot ID is one another server keeps
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1456 — Say in restore_server's refusal when the snapshot ID is one another server keeps

## Goal

`restore_server` given a snapshot ID that the named server does not keep, but another running server
does, answers that snapshots restore only into the server that took them and names the server that
keeps it. It no longer blames the four-snapshot limit.

## Context

- Surly bug report `2026-11-19T2203-Surl-bug-report.md` item 5, Low (Surl `763754785`, unchanged since):
  "`restore_server` given another server's snapshot answers 'only its newest 4 snapshots are kept'".
- `Application/Surl.Mcp.UnitLibrary/RestoreServerTool.cs:46-49` (`NoSnapshot`) always says
  `server <id> has no snapshot <snapshot>; only its newest 4 snapshots are kept`, and `:53-57` looks only
  in `running.Snapshots` (`ServerSnapshots`, one per server).
- Snapshot IDs are numbered per server (`ServerSnapshots.Keep`, `p{++lastSnapshotNumber}`), so `p1`
  may exist on several servers. The tool can only tell "another server keeps an ID like this" by asking
  each running server in `RunningSurlServers`. When none does, the text says the ID was never taken of
  this server or was dropped as its oldest.
- `Surl.Api`'s own `RestoreAsync` with another server's `SurlSnapshot` object already says "The snapshot
  was taken by another server" (`SurlServer.cs:252`); the tool never reaches it because it looks up by ID.

## Acceptance criteria

- [ ] With servers `s1` and `s2` running and only `s2` keeping `p2`, `restore_server s1 p2` answers an
      `isError` result naming `s2` and saying a snapshot restores only into the server that took it
      (named test in `Application/Surl.Mcp.UnitTests`).
- [ ] With no running server keeping the ID, the text says it was never taken of that server or was
      dropped as one past the newest 4 (named test).
- [ ] The tool's description and `RestoreServerTool`'s XML summary match the new behaviour.
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean, the fast tests are green,
      and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T2203-Surl-bug-report.md` item 5.
- 2026-11-20: Backlog -> Doing.
