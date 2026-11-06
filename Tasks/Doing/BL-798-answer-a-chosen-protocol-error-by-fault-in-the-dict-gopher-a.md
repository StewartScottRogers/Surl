---
id: BL-798
title: Answer a chosen protocol error by fault in the DICT, Gopher and TELNET servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-798 — Answer a chosen protocol error by fault in the DICT, Gopher and TELNET servers

## Goal

When a chosen-error fault rule matches, the DICT server answers the matching command with the rule's status code
and text, the Gopher server answers the matching selector with an error item (type `3`) carrying the rule's text,
and the TELNET server does what the ADR decides for a protocol with no error answer, so a test can make one lookup
fail on purpose.

## Context

- Decision: BL-822's ADR decision 4 (what "command" and "path" mean here; for TELNET, which has no error reply,
  the planning default is "write the rule's text as one line and close"; if the ADR instead refuses `error` rules
  for `telnet`, this task's TELNET part becomes a test that the server ignores such a rule, and the refusal belongs
  to BL-785).
- Contract: BL-823 (`ExchangeContext.Faults`, `FaultAppliedEvent`). Tests use BL-823's scripted fault plan and
  recording publisher with `InMemoryConnection`.
- Where: `Surl.Protocol.Dict`, `Surl.Protocol.Gopher`, `Surl.Protocol.Telnet`. None has a login, so an
  account-scoped rule never matches them.
- Request bytes from pinned upstream curl 8.21.0 (`Record-CurlExchange.ps1`), never the Curl port (ADR-0003).
  Record in Notes the exit code upstream curl 8.21.0 gives for each named case.
- Stand-in: no store change; visible as a `FaultAppliedEvent`.

## Acceptance criteria

- [ ] Named tests: DICT `DEFINE * word` under a rule gets the rule's status line; Gopher selector `/a` under a rule
      gets the error item while `/b` is served; TELNET under a rule behaves as the ADR says; each publishes one
      `FaultAppliedEvent` (or, for a refused TELNET rule, none).
- [ ] With no rule, every existing test of the three libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the three libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
