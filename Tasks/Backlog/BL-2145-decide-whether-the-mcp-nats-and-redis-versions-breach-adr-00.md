---
id: BL-2145
title: Decide whether the MCP, NATS and Redis versions breach ADR-0006's no-version banner rule
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2126]
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: NFR-019
created: 2026-11-28
completed:
---
# BL-2145 — Decide whether the MCP, NATS and Redis versions breach ADR-0006's no-version banner rule

## Goal

ADR-0006 section 3 and NFR-019's target agree with the versions surl serves, or a decision says which of them is to stop being served.

## Context

Found while working BL-2120 (2026-11-28). ADR-0006 section 3 says a server never states surl's, .NET's or the operating system's version, and NFR-019's target says "0 version numbers in any banner". Served today: the MCP `serverInfo.version` is surl's informational version (ADR-0111; ADR-0103 for `surl-mcp`; `McpServerIdentity`), the NATS `INFO` carries a fixed `version` (`NatsInfo`, ADR-0113) and Redis `HELLO` a fixed one (`RedisConnectionCommands`, ADR-0106). The fixed ones are not surl's, .NET's or the operating system's, and clients read them. Search the other servers for any further version they send before deciding. Decide by the standing rules (root `CLAUDE.md`, "Decisions"). The expected answer is to amend the ADR and the target to what is served, but record whichever follows. Stand-in: a client library in a test may require these fields to connect, and a deterministic fixed value keeps its assertions stable.

## Acceptance criteria

- [ ] A dated amendment of ADR-0006 section 3, marked "Decided by Claude under Stewart's delegation", says which versions a peer may be sent, naming MCP, NATS, Redis and any other found, with the alternative rejected and why.
- [ ] NFR-019's target says the same rule, and its status no longer says it waits on the banner clause matching what is served.
- [ ] NFR-019's status stays `Draft` with what it still waits on (a check across every server) in brackets, unless such a check exists.
- [ ] If the decision is that something served must change, the implementation task is filed in `Tasks/Backlog`, its ID is in this task's Log and NFR-019's brackets name it.
- [ ] No file outside `Documentation/` and `Tasks/` changes.

## Notes

## Log

- 2026-11-28: Created.
