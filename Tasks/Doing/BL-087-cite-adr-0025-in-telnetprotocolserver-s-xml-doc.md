---
id: BL-087
title: Cite ADR-0025 in TelnetProtocolServer's XML doc
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-077]
touches: [Surl.Protocol.Telnet.UnitLibrary]
requirement: none
created: 2026-09-29
completed:
---
# BL-087 — Cite ADR-0025 in TelnetProtocolServer's XML doc

## Goal

`TelnetProtocolServer`'s XML doc names ADR-0025 as the record of its behaviour, and its
"Limits" paragraph no longer cites ADR-0006 section 5 for the `line too long` answer,
which ADR-0025 decision 5 records as a departure from that section.

## Context

- BL-077 wrote ADR-0025 (how the TELNET server answers) from BL-035's decisions. The XML
  doc of `Surl.Protocol.Telnet.UnitLibrary/TelnetProtocolServer.cs` predates it and does
  not cite it.
- ADR-0006 section 5 says TELNET closes with no bytes when a line is too long; the server
  answers `line too long` first, and the doc's "(ADR-0006, sections 1 and 5)" reads as if
  section 5 said so. ADR-0025 decision 5 records the departure and why.
- No behaviour change.

## Acceptance criteria

- [ ] `TelnetProtocolServer`'s `<remarks>` cite ADR-0025.
- [ ] The "Limits" paragraph cites ADR-0006 section 1 for the limit and ADR-0025 for the
      `line too long` answer, and no longer implies ADR-0006 section 5 prescribes it.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
