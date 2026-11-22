---
id: BL-1591
title: Decide how the Exchange opened note names its listener
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md]
requirement: none
created: 2026-11-22
completed:
---
# BL-1591 — Decide how the Exchange opened note names its listener

## Goal

ADR-0033 gains an amendment, decided by Claude under Stewart's delegation, that fixes the exact text of the
`Exchange <id> opened` note once it names the listen URL that accepted the exchange, so `-v`, `--trace-ascii` and
`--log-file` tell two listeners of one scheme apart.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T0726-Surl-bug-report.md` item 4: with
  `surl -v --directory site http://127.0.0.1:8080/ http://127.0.0.1:8081/`, both lines read
  `#N * Exchange N opened: http from 127.0.0.1:...`; the control endpoint's `exchange_started` events do name the
  listener.
- The text is decided, not drifted: ADR-0033 section 1 lists `Exchange <id> opened: <scheme> from <remote>.` and
  section 3 ("The info line") pins `#<id> * Exchange <id> opened: <scheme> from <remote>.` with examples, and its
  table writes notes starting `Exchange <id> opened: ` at `info`. ADR-0007 section 8's older list is superseded by
  ADR-0033 (ADR-0007's header). So changing the note is an ADR amendment first.
- The code: `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:1003` writes the note; the event on the next line uses
  `ReportedListenUrl.Format(listenUrl)` (the bound port, as `Listening on` prints it). One form serves connections
  and datagram flows.
- Constraints the decision must respect: keep the prefix `Exchange <id> opened: ` so
  `Foundation/Surl.Output.UnitLibrary/LevelledExchangeLogFactory.cs:91` still writes it at `info`, and
  `Conformance/Surl.Conformance.UnitTests/CurlPortRunSetup.cs:105`'s regex `\* Exchange \d+ opened` still matches;
  a listen URL is operator configuration, not a peer secret (ADR-0182 does not apply), but a Unix-socket listen URL
  must still print safely (ADR-0006 section 3 escaping).
- Upstream curl has no server log to mirror; this is Surl's own log (ADR-0033). Stand-in: the log is what a person
  reads; tests use `ExchangeStartedEvent.ListenUrl`, which already carries it.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md` has a new
      `## Amendment` section, marked "Decided by Claude under Stewart's delegation", stating the note's exact new text
      (for example `Exchange <id> opened: <scheme> on <listen URL> from <remote>.`, or the form it chooses and why) and
      one example each for a connection and a datagram flow.
- [ ] The amendment states that the `Exchange <id> opened: ` prefix and its `info` level are unchanged, that the
      listen URL is printed as `ReportedListenUrl.Format` gives it (bound port), and how a Unix-socket listen URL prints.
- [ ] It names BL-1592 as the task that changes the code and tests.
- [ ] Sections 1 and 3 of ADR-0033 point to the amendment where they quote the old text.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
