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
completed: 2026-09-29
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

- [x] `Documentation/Wiki/Glossary.md` has entries for "request head", "request line"
      and "field line", each citing its RFC 9112 section and naming the
      `Surl.Protocol.Http` type that holds it.

## Notes

- Added a new "HTTP" section to the glossary (between "Serving" and "Building and testing") rather than folding the terms into "Serving": they are protocol-specific, and later HTTP terms (status line, message body framing) have a place to go.
- Each entry names both the type that holds the concept and the parser that produces it, since `HttpRequestLineParser` and `HttpFieldLineParser` carry the same names.
- Docs only; no `.cs` or project file changed, so the verify skill was not needed.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The glossary defines request head, request line and field line, each with its RFC 9112 section and Surl.Protocol.Http type
