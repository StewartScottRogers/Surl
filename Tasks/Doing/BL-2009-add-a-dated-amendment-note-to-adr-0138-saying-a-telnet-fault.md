---
id: BL-2009
title: Add a dated amendment note to ADR-0138 saying a TELNET fault point also carries the listen URL and exchange ID
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0138-how-the-dict-gopher-and-telnet-servers-answer-a-chosen-error.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2009 — Add a dated amendment note to ADR-0138 saying a TELNET fault point also carries the listen URL and exchange ID

## Goal

A reader of ADR-0138 and of its row in the decision index learns that a `listener`-scoped rule can match a TELNET
line, and that `delay` and `drop` at a command are applied.

## Context

Found while working BL-1986 (2026-11-27), read from source. Documentation only; no code or behaviour change.

Decision 3 says the TELNET plan is asked "with scheme `telnet` and nothing else - no command, path or account".
`TelnetLineFaults.FindAndApplyAsync` (`Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary`) also sets the point's
`ListenUrl` and `ExchangeId`, `FaultPlan` (`Foundation/Surl.Core.UnitLibrary`) matches a rule's `listener` against
that URL, and `TelnetLineFaultsTests` pins the whole point. So a listener-scoped rule does match a TELNET line; one
scoped by command, path or account never does. Decision 6 already says the point carries the exchange ID, the listen
URL and the scheme, so it contradicts decision 3's "nothing else".

DICT and Gopher, checked the same day: decisions 1 and 2 also leave the listen URL and exchange ID unnamed, though
`DictCommandFaults` and `GopherSelectorFaults` set both. Neither says "nothing else", and what each says is absent
(DICT: account; Gopher: command and account) is absent, so they are incomplete but not false, and decision 6 covers
them. One note naming all three servers is enough; decisions 1 and 2 need none of their own.

The ADR's row in `Documentation/Planning/Decisions/README.md` is stale too. It says TELNET asks "with the scheme
alone", and that "a `delay` or `drop` at a command, is noted and not applied (BL-914)". BL-914 made all three servers
apply both, as decision 5's amendment says.

Convention: the ADR's recorded text is not rewritten. Add a header bullet `- **Amended (<date>, BL-1999):** ...` and
a dated italic note under decision 3, as BL-1959 did in ADR-0107. The index row is a summary and is corrected in
place.

## Acceptance criteria

- [ ] A dated note under decision 3 says the point also carries the listen URL and exchange ID (decision 6), so a rule scoped by `listener` or `scheme` can match a TELNET line and one scoped by command, path or account never does.
- [ ] The same note says the DICT and Gopher points of decisions 1 and 2 carry the listen URL and exchange ID too.
- [ ] ADR-0138's header has a dated `Amended` bullet pointing at that note.
- [ ] The diff to ADR-0138 only adds lines.
- [ ] The index row for 0138 no longer says "with the scheme alone", and says a command-stage `delay` or `drop` is applied (BL-914) and only a reply that does not fit, or a kind other than `error`, `delay` or `drop`, is noted and not applied.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
