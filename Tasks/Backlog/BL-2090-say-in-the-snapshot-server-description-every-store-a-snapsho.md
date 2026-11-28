---
id: BL-2090
title: Say in the snapshot_server description every store a snapshot copies and what it leaves out
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2090 — Say in the snapshot_server description every store a snapshot copies and what it leaves out

## Goal

An agent reading the `surl-mcp` tool list learns which stores `snapshot_server` copies and which state it leaves out.

## Context

Found while working BL-2079, read from source on 2026-11-28. `SnapshotServerTool.Description` names files, mail,
messages and accounts only. `SurlServer.SnapshotAsync`'s XML documentation (`Application/Surl.Api/SurlServer.cs`)
lists what a snapshot copies (also proxy accounts, TLS-SRP accounts, Redis keys, LDAP entries, AMQP 0-9-1 exchanges
and bindings, Kafka producer state, MQTT 5 sessions kept with no connection and DNS zones) and what it does not
(uploads, OCI tokens, MCP OAuth state and HTTP sessions, Kafka consumer groups, MQTT 5 pending Wills, queued
deliveries and unacknowledged publishes). `restore_server`'s description (BL-2079) is the wording to match.

## Acceptance criteria

- [ ] `SnapshotServerTool.Description` names every store a snapshot copies and the state it does not copy, and a test in `Application/Surl.Mcp.UnitTests` asserts it.
- [ ] `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`'s `snapshot_server` entry says the same.

## Notes

## Log

- 2026-11-28: Created.
