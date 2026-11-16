---
id: BL-1218
title: Read the Redis keyspace back through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint (ADR-0164 decision 9)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: [Surl.Extended.Protocol.Redis.UnitLibrary, Surl.Extended.Protocol.Redis.UnitTests, Surl.Composition.UnitLibrary, Surl.Composition.UnitTests, Surl.Api, Surl.Api.UnitTests, Surl.Mcp.UnitLibrary, Surl.Mcp, Surl.Mcp.UnitTests, Surl.Control.UnitLibrary, Surl.Control.UnitTests]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-1218 — Read the Redis keyspace back through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint (ADR-0164 decision 9)

## Goal

A test reads the Redis string keys back, listed by glob or one by one, through all three hosts.

## Context

ADR-0164 decision 9 (decisions 2 to 4 apply unchanged); the store is seeded by ADR-0158. Set `touches` at claim time to the projects that hold the store, `Surl.Composition`, `Surl.Api`, `Surl.Mcp.UnitLibrary` and `Surl.Control.UnitLibrary`, or split the task if the board says they overlap.

## Acceptance criteria

- [x] The store is readable through `ComposedStores`, `Surl.Api`, `Surl.Mcp` and the control endpoint with the member, tool and route names ADR-0164 decision 9 gives; a read publishes no event, consults no fault and returns no secret.
- [x] Unit tests hold every touched library at the quality gates; `--aihelp` and docs name the new reads.

## Notes

- touches was empty; set it to the eleven projects the work changed (no other task was in Doing). Redis library added because the read must not mutate (no expiry sweep, no journal) so `RedisKeyspace.ReadKey`/`ListKeys` and `RedisDatabase.Peek`/`PeekLive` were needed.
- Names follow ADR-0164 decision 9: `Keys.GetAsync`/`ListAsync`, tools `read_key`/`list_keys`, routes `GET /v1/keys/{key}` and `GET /v1/keys`; `database` (default 0) is an optional extra on each, and a key outside UTF-8 is shown as `key_base64`. A listing never holds a value; a non-string key is listed with its type and has no value on a read. Paging by `after_key` is UTF-8 text on every host.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Redis keys read back through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint
