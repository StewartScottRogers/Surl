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
completed:
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

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation", citing Stewart's approval of the category. It adds the
      `Protocols/DataStore/` row to ADR-0079 decision 1's table, says why Redis does not belong in `Messaging/`,
      and is listed in the folder's `README.md`.
- [ ] ADR-0079 carries an "Amended by" line pointing to the new ADR.
- [ ] `Documentation/Product/Product-Overview.md`, "Project layout", names `DataStore/` among the `Protocols/`
      folders.

## Notes

## Log

- 2026-11-04: Created.
