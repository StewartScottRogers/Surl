---
id: BL-058
title: Add request head and field line to the glossary
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-017]
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-058 — Add request head and field line to the glossary

## Goal

`Documentation/Wiki/Glossary.md` defines the HTTP parsing terms BL-017 introduced, so
every later HTTP task uses one name for each concept.

## Context

- BL-017 added `HttpConnectionReader`, `HttpRequestHead`, `HttpRequestField` and
  `HttpRequestHeadReadOutcome` to `Surl.Protocol.Http.UnitLibrary`. Its code review noted
  that "request head" and "field line" have no glossary entry.
- The terms come from RFC 9112: sections 2 and 3 for the request line and head, and
  section 5 for the field line.

## Acceptance criteria

- [ ] `Documentation/Wiki/Glossary.md` has entries for "request head", "request line"
      and "field line", each citing its RFC 9112 section and naming the
      `Surl.Protocol.Http` type that holds it.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
