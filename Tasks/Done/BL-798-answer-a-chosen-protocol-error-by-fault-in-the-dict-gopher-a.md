---
id: BL-798
title: Answer a chosen protocol error by fault in the DICT, Gopher and TELNET servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests, Documentation/Planning/Decisions/ADR-0138-how-the-dict-gopher-and-telnet-servers-answer-a-chosen-error.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-05
completed: 2026-11-05
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

- [x] Named tests: DICT `DEFINE * word` under a rule gets the rule's status line; Gopher selector `/a` under a rule
      gets the error item while `/b` is served; TELNET under a rule behaves as the ADR says; each publishes one
      `FaultAppliedEvent` (or, for a refused TELNET rule, none).
- [x] With no rule, every existing test of the three libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the three libraries keep 100% line and
      branch coverage.

## Notes

Delivered (2026-11-05, dark factory lane 2) under ADR-0138 (decided by Claude under Stewart's delegation;
0136 was already taken by another lane and 0137 left for BL-797's lane, which is deciding the same question for
MQTT and LDAP at the same time).

- DICT (`DictCommandFaults`): asked once per recognized command, before its own checks; path the word of
  `DEFINE`/`MATCH` or the database of `SHOW INFO`; the reply's status line answers, the connection stays open.
- Gopher (`GopherSelectorFaults`): asked once the selector is read, before the content store; path the request
  path (`/a`); answered `3<reply>\t\terror.host\t1\r\n.\r\n`, then writes completed.
- TELNET (`TelnetLineFaults`): ADR-0124's planning default kept - asked per completed line with the scheme only
  (no command, path or account exist); the reply is sent as one line in place of the echo and the session ends.
  `TelnetSession.Receive` now stops at each completed line (`LineAwaitingAnswer`) so the server can ask the
  plan asynchronously. Event target `line <n>`, never the line's text.
- A reply that does not fit the scheme's shape (a `Surl.Api` hook can hand over anything), or a `delay`/`drop`
  rule at the command stage, is noted and not applied, as ADR-0135 decisions 4 and 5. Neither BL-901 (mail)
  nor BL-909 (SSH, SMB) covers these three servers, so filed **BL-912** for their command-stage delay and drop.
- The acceptance criterion's Gopher `/b` is checked as `/file.txt` being served under a rule for `/a`
  (`ServeAsync_SelectorTheRuleDoesNotName_IsServed`), since `/b` does not exist in the test content.

Exit codes from pinned upstream curl 8.21.0 (win-x64), recorded with `Record-CurlExchange.ps1` into each test
project's `Fixtures/` (READMEs updated):

| Case | Fixture | Exit |
| --- | --- | --- |
| DICT `DEFINE * word` answered `554 try later` | `fault-define` | 0 (curl prints the status line) |
| Gopher selector `/a` answered with the `3try later` error menu | `fault-selector` | 0 (curl prints the menu) |
| TELNET line 1 (`hello`) answered `try later`, then closed | `fault-line` | 0 (curl prints banner and line) |

Tests: `DictCommandFaultsTests`, `GopherSelectorFaultsTests`, `TelnetLineFaultsTests`. Dict 162, Gopher 95,
Telnet 87 tests; the three libraries at 100% line and 100% branch (Cobertura from the MSTest collector).
`touches` grew by ADR-0138 and the decisions README: no task in Doing names them.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. DICT, Gopher and TELNET answer a matching chosen-error fault rule (status line, error item, one line then close) and publish FaultAppliedEvent
