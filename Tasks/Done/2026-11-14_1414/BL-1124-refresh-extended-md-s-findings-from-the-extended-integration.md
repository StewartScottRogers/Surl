---
id: BL-1124
title: Refresh Extended.md's findings from the extended integration tasks now Done
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Conformance/CurlPort/Extended.md, Documentation/Conformance/CurlPort/README.md]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-1124 — Refresh Extended.md's findings from the extended integration tasks now Done

## Goal

`Extended.md` records every extended client's Integration result, not four of nineteen.

## Context

`Extended.md` (dated 2026-11-11, BL-592) records 4 of the 19 extended clients (BL-456, BL-462, BL-477, BL-541) and lists BL-422 to BL-502 as not done. The board shows all of those Integration tasks Done. Read each Done task's Notes, add its port defects as EXT findings, and update the Port-Board-Handoff only if a finding is new.

## Acceptance criteria

- [x] The "Extended tasks not yet done" table is gone or empty, and every Done Integration task has a row in the findings table.

## Notes

- Docs only. Added EXT-2 to EXT-6 (grpc -u, signalr 401, mqtt5 0x86, amqp -T, kafka auto-create) from the Done tasks' Notes, handoff entries 19 to 23 (new findings), README counts. BL-515 and BL-534 Notes are empty, so listed as no defect recorded.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. Extended.md lists all 19 clients, EXT-2 to EXT-6 added, handoff 19-23
