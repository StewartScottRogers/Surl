---
id: BL-305
title: Document the WebSocket server in the product overview, glossary, README, roadmap and requirements
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-304, BL-320]
touches: [Documentation/Product/Product-Overview.md, Documentation/Wiki/Glossary.md, README.md, Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md, Surl.Protocol.Ws.UnitLibrary/CLAUDE.md]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
---
# BL-305 — Document the WebSocket server in the product overview, glossary, README, roadmap and requirements

## Goal

The repository's documents say, truly, that Phase 4 is built: what `surl ws://` and `surl wss://`
answer and what pinned upstream curl has proven against them.

## Context

- Pattern: BL-214 (Phase 3's documentation task).
- Sources: BL-281's and BL-285's ADRs, the code as built by BL-288, BL-293, BL-301 to BL-303, and
  BL-304's conformance tests.
- What each file gains:
  - `Documentation/Product/Product-Overview.md`: a "Built for Phase 4: WebSocket" section in the shape
    of "Built for Phase 3", the "Also in scope" authentication list naming the WebSocket upgrade's
    challenges, "Layers" and "Project layout" naming `Surl.HttpMessage` as built.
  - `Documentation/Wiki/Glossary.md`: WebSocket terms the code uses (upgrade, frame, message,
    control frame, close code, masking), each with its code column.
  - `README.md`: `ws` and `wss` among the served schemes.
  - `Documentation/Planning/Roadmap.md`: a "Milestone 4 — Phase 4" section with status, what it
    delivers, exit criteria and decisions, as Milestone 3's is written.
  - `Documentation/Product/Requirements.md`: FR-048 (and FR-052's WebSocket part) restated as built,
    ending with what satisfies it.
  - `Surl.Protocol.Ws.UnitLibrary/CLAUDE.md`: what the library holds and references now.
- Every statement true of the code as it is now; anything not built is written as intent.

## Acceptance criteria

- [x] Each file in Context carries the section or rows it names, and every ADR, type and test it cites
      exists.
- [x] No document claims a behaviour BL-304's tests do not prove or the code does not have.

## Notes

- align-and-document wrote the six touched files in BL-214's shape, from ADR-0070, ADR-0071 (with
  Amendment 1), the code in `Surl.Protocol.Ws`, `Surl.HttpMessage` and `Surl.Console`'s
  `ComposeProtocolServers`, and BL-304's `UpstreamCurlTalksToSurlOverWebSocketTests` (20 tool
  rows) and `PinnedLibcurlTalksToSurlOverWebSocketTests` (7 libcurl rows, Windows only).
- Product overview: "Built for Phase 4: WebSocket"; the WebSocket upgrade's logins in "Also in
  scope"; Rule 1, "Layers" and "Project layout" name `Surl.HttpMessage` as built. Glossary: a
  "WebSocket" section (WebSocket upgrade, accept key, frame, opcode, message, control frame,
  close code, masking, lingering close, echo), plus `websocket` in "help category", the
  WebSocket server in "implicit TLS scheme" and in "request head". README: `ws` and `wss` in the
  served schemes, the upgrade in "Logging in", and a "WebSocket" section with examples. Roadmap:
  Milestone 4; "Later" now names Phases 5 and 6. Requirements: FR-048 and FR-052's WebSocket
  part restated as built (status kept `Draft`, as FR-036 to FR-047 are).
- Choice: the Linux and macOS CI results are not recorded against ADR-0071, so the documents
  say so rather than "passed", as BL-214 did.
- Found, not fixed here (outside `touches`): ADR-0071 decision 6 refuses a client `CLOSE` code
  1012 to 2999 with 1002, but `WebSocketCloseCodes.IsAllowedOnTheWire` (BL-288's default, from
  the IANA registry) allows 1012 to 1014, so surl echoes them. The documents state the code's
  set; filed BL-339 to amend the ADR (the IANA registry is the current specification, so the
  code stays).
- Lane 2 (second attempt): cherry-picked lane 1's docs commit from
  factory/BL-305-lane-1-20260930-162236 onto the current board; it applied cleanly and every
  ADR, type and test it cites still exists. Lane 1's follow-up was numbered BL-338, which another
  lane has since taken, so it is refiled as BL-339.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Backlog. Lane 1 could not integrate: push kept being refused. The work is on branch factory/BL-305-lane-1-20260930-162236; start with git cherry-pick --no-commit factory/BL-305-lane-1-20260930-162236 and fix it.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Product overview, glossary, README, roadmap, requirements and the Ws library's CLAUDE.md describe the WebSocket server as built and as pinned upstream curl proves it
