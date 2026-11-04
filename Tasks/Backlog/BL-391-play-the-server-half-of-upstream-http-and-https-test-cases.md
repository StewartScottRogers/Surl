---
id: BL-391
title: Play the server half of upstream http and https test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-388]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-391 — Play the server half of upstream http and https test cases

## Goal

Surl plays the server half of upstream test cases whose `<server>` is `http` or `https`, as BL-386's
ADR decision 2 defines it. It answers each request with the case's `<reply>` (`<data>`, `<dataNUM>`,
`<connect>`) and the `<servercmd>` instructions that the ADR keeps, and it records the bytes it
received for `<protocol>`.

## Context

- Decision: BL-386's ADR, decision 2. It fixes the meaning, and where the code lives. `touches`
  assumes `Surl.Conformance` and `Surl.Protocol.Http`. If the ADR puts the code somewhere else, work
  there and say so in Notes.
- How upstream's HTTP server chooses a reply: FILEFORMAT.md (tag `curl-8_21_0`) `<data>`,
  `<dataNUM>` ("the server picks this part when the request path ends in a NUM"), `<connect>`, and the
  HTTP `<servercmd>` list (lines 397 to 414). The overview's "Constraints" say that Surl is written from
  documentation and measurement, not by translating upstream's test servers. Read FILEFORMAT.md and
  the protocol RFCs, not `tests/server/sws.c`.
- The record of received bytes is what BL-390's `<protocol>` check compares. Its shape is the one
  BL-390 consumes.
- No network in unit tests: the server half is driven through `InMemoryConnection`.

## Acceptance criteria

- [ ] Unit tests drive the server half with inline cases: a single `<data>` reply, a `<dataNUM>`
      chosen by the request path's number, `swsclose`, `idle`, `writedelay` with a fake
      `TimeProvider`, `skip`, and `auth_required`. The tests check the bytes sent and the bytes
      recorded.
- [ ] Each `<servercmd>` the ADR excludes produces the named skip reason, not a silent pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
