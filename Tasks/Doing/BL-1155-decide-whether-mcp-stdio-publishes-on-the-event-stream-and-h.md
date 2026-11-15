---
id: BL-1155
title: Decide whether mcp+stdio publishes on the event stream and honours fault rules
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-060
created: 2026-11-14
completed:
---
# BL-1155 — Decide whether mcp+stdio publishes on the event stream and honours fault rules

## Goal

A new ADR, marked "Decided by Claude under Stewart's delegation", decides whether `surl mcp+stdio:` - today served outside the serving engine - publishes its exchange on the event stream and honours fault rules, and files the tasks that do it if so.

## Context

- Stand-in audit 2026-11-14: `Application/Surl.Composition.UnitLibrary/McpStdioComposition.ServeAsync` builds a content store and calls `McpStdioSession.RunAsync` directly. No `ServingEngine`, so no `ExchangeStartedEvent`, bytes or end, no `ExchangeContext.Faults`, no `IIdentifierRandom`, and the `ComposedServing` event stream, store control and control endpoint do not exist for it (`FindRefusal` refuses any other listen URL beside it). Every other scheme is observable and fault-injectable (ADR-0124 decisions 1 and 4).
- ADR-0111 decision 5 is where mcp+stdio was decided; ADR-0124's principles are the yardstick. Possible answers include: publish the session as one exchange on a stream composed as for any run, so `--control` works beside it; or record that standard input and output is a single process-owned session a test drives directly, and why the principles do not reach it.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` (next free number) decides, with reasons, whether mcp+stdio publishes on the event stream, honours `--fault` rules (which kinds, at which points) and may be combined with `--control`.
- [ ] If any of it is to be built, this task files one task per library touched with `task-board.ps1 new`, exact `touches`, `depends-on` this task, and checkable acceptance criteria, and the ADR lists them under "Acted on by".
- [ ] `Documentation/Product/Requirements.md` FR-060's text stays true: if mcp+stdio stays outside the stream, FR-060 says so in one clause.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
