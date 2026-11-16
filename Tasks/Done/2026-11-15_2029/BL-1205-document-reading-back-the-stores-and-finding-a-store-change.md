---
id: BL-1205
title: Document reading back the stores and finding a store change by name
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1199, BL-1200, BL-1201, BL-1202, BL-1203, BL-1204]
touches: [Documentation/Wiki/Surl-As-A-Stand-In.md, Application/Surl.Api/README.md, Application/Surl.Mcp/README.md, Documentation/Product/Requirements.md]
requirement: FR-061
created: 2026-11-15
completed: 2026-11-15
---
# BL-1205 — Document reading back the stores and finding a store change by name

## Goal

The stand-in page, both host READMEs and the requirements say how a test reads back a store and finds a store change by name, true to the code as BL-1199 to BL-1204 built it.

## Context

- Decision: the ADR filed by BL-1194. Built by BL-1199 and BL-1200 (`Surl.Api`), BL-1201 and BL-1202 (`Surl.Mcp`), and BL-1203 and BL-1204 (control endpoint).
- BL-1172 and BL-1177 documented seeding and the control endpoint the same way. Follow their layout.

## Acceptance criteria

- [x] `Documentation/Wiki/Surl-As-A-Stand-In.md`, section "Controlling: seed, reset, snapshot, restore" (or a new "Reading back" section after it), shows one `Surl.Api` read example for mail, files and the bus. Section "Observing" shows a query with `Store` and the name filter. Section "The control endpoint" lists the new routes and query keys, and section "`Surl.Mcp`, for AI agents" lists the new tools.
- [x] `Application/Surl.Api/README.md` documents each new member and the name filter, with the bounds and not-found results the ADR states.
- [x] `Application/Surl.Mcp/README.md` documents each new tool and the `store` and name properties of `get_events` and `wait_for_event`.
- [x] `Documentation/Product/Requirements.md` FR-061 states that a test reads back the mail store, the content store and the bus, and FR-060 states the store and name filters, each naming the tasks that built them.
- [x] Every member, tool, route and query key named in these documents exists in the code under that name.

## Notes

- Added a "Reading back what a client left" section to the stand-in page (after Controlling), the Name query to Observing, the routes and tools to the control endpoint and Surl.Mcp sections. FR-060 and FR-061 name BL-1200/1202/1204 and BL-1199/1201/1203. Every member, tool, route and query key was checked against the code.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Docs for reading back stores and finding a store change by name
