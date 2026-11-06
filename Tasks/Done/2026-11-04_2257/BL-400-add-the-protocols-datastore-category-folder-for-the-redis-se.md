---
id: BL-400
title: Add the Protocols/DataStore category folder for the Redis server
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543]
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-400 — Add the Protocols/DataStore category folder for the Redis server

## Goal

An ADR amends ADR-0079 to add the `Protocols/DataStore/` category folder, where the Redis server
`Surl.Extended.Protocol.Redis` and any later data-store server go.

## Context

- ADR-0079 decision 3: "A new category is decided by an ADR." Stewart approved `Protocols/DataStore/` for Redis
  on 2026-11-04. That approval is recorded in BL-543's ADR.
- ADR-0079 decision 2: categories are by job, not by wire shape. Redis is a data store. It is neither a message
  broker (`Messaging/`) nor a lookup service (`Lookup/`), although RESP is a request-reply protocol and Redis
  has pub/sub.
- ADR-0079 decision 4: `Surl.slnx` mirrors the disk, one solution folder per category. The `/Protocols/DataStore/`
  solution folder is created by the Redis scaffold task (BL-488), not here: this task changes no project.
- The product overview's "Project layout" lists the category folders.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation", citing Stewart's approval of the category. It adds the
      `Protocols/DataStore/` row to ADR-0079 decision 1's table, says why Redis does not belong in `Messaging/`,
      and is listed in the folder's `README.md`.
- [x] ADR-0079 carries an "Amended by" line pointing to the new ADR.
- [x] `Documentation/Product/Product-Overview.md`, "Project layout", names `DataStore/` among the `Protocols/`
      folders.

## Notes

- ADR-0095 is the next free number (none in the other lane worktrees either). Docs only: no `.cs` or project file changed, so no build or test run was needed for this diff.
- The folder is not created empty: BL-488 makes it with the Redis projects and the `/Protocols/DataStore/` solution folder, and, by an acceptance box and `touches` entry added to BL-488 here, adds it to the root `CLAUDE.md` tree then (`CLAUDE.md` is outside this task's touches, and its tree lists only folders that exist).
- The object stores stay in `Protocols/FileTransfer/`, as ADR-0083 places them.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. ADR-0095 adds the Protocols/DataStore/ category folder for the Redis server, amending ADR-0079; the product overview names it
