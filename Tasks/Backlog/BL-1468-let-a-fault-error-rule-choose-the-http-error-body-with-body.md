---
id: BL-1468
title: Let a fault error rule choose the HTTP error body with body= (ADR-0186)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1444]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1468 — Let a fault error rule choose the HTTP error body with body= (ADR-0186)

## Goal

A `--fault error;scheme=http;reply=<status>;body=<text>` rule makes the HTTP server answer that status with that text as its body.

## Context

- ADR-0186 decision 5; ADR-0124 decision 4 for `reply=`; `HttpRequestAnswerer.Faults.cs` is where fault answers are built.

## Acceptance criteria

- [ ] `body=` is optional; without it the answer carries ADR-0186's fixed body for the status.
- [ ] A `body=` value is sent as `text/plain; charset=utf-8` with the right `Content-Length` and none on HEAD.
- [ ] `--aihelp` facts and example describe it.

## Notes

## Log

- 2026-11-20: Created.
