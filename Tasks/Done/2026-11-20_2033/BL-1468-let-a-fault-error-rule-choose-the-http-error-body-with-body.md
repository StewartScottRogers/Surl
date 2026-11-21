---
id: BL-1468
title: Let a fault error rule choose the HTTP error body with body= (ADR-0186)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1444]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Mcp.UnitLibrary]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1468 — Let a fault error rule choose the HTTP error body with body= (ADR-0186)

## Goal

A `--fault error;scheme=http;reply=<status>;body=<text>` rule makes the HTTP server answer that status with that text as its body.

## Context

- ADR-0186 decision 5; ADR-0124 decision 4 for `reply=`; `HttpRequestAnswerer.Faults.cs` is where fault answers are built.

## Acceptance criteria

- [x] `body=` is optional; without it the answer carries ADR-0186's fixed body for the status.
- [x] A `body=` value is sent as `text/plain; charset=utf-8` with the right `Content-Length` and none on HEAD.
- [x] `--aihelp` facts and example describe it.

## Notes

- touches widened (no other task in Doing): body= is a new fault key, so FaultRule (Abstractions), the reader and AiHelp (Cli), Composition, Api and Mcp carry it too.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. A --fault error rule can choose the HTTP error body with body=<text>; without it the fixed text stays
