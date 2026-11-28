---
id: BL-2157
title: Re-state NFR-014 to NFR-016 once 0 means no limit in every server
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2148, BL-2149, BL-2150, BL-2151, BL-2152, BL-2153, BL-2154, BL-2155, BL-2156]
touches: [Documentation/Product/Requirements.md]
requirement: NFR-014
created: 2026-11-28
completed:
---
# BL-2157 — Re-state NFR-014 to NFR-016 once 0 means no limit in every server

## Goal

The statuses of NFR-014, NFR-015 and NFR-016 in `Documentation/Product/Requirements.md` say what holds "0 for no limit" after the fixes, naming only checks that exist.

## Context

BL-2148 marked the three rows `Draft` because some servers compared a limit of 0 as given. BL-2149 (Redis), BL-2150 (NATS), BL-2151 (STOMP), BL-2152 (XMPP), BL-2153 (`coaps` handshake), BL-2154 and BL-2155 (`mcp+stdio:`) and BL-2156 (WebTransport) fix them. The section preamble allows `Enforced by` an existing gate, or `Draft` with what it waits on in brackets. Before restating, search the production code for `MaxLineBytes`, `MaxMessageBytes` and `MaxUploadBytes` and check each use treats 0 as no limit; one to look at is `SocketIoSession.OpenPacket`, which writes `--max-message` as the Engine.IO `maxPayload`, 0 included (check it against ADR-0120). A use that still fails is filed as its own task and keeps the row `Draft`. NFR-016 also states that a partial upload over the limit is deleted. Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [ ] NFR-014's status no longer says the Redis, NATS or STOMP servers hold 0 as a limit, and names the test classes that hold 0 for them.
- [ ] NFR-015's status no longer says STOMP, NATS, XMPP, `coaps` or `mcp+stdio:` fail at 0, and names the test classes that hold 0 for them.
- [ ] NFR-016's status no longer says the Redis server or a WebTransport session fails at 0, and names the test classes that hold 0 for them.
- [ ] Each of the three statuses is `Enforced by` an existing gate, or `Draft` with what it waits on (a filed task's ID where one was needed) in brackets.
- [ ] Every test class named exists in the repository, and the file's `Last updated` date is that of the change.

## Notes

## Log

- 2026-11-28: Created.
