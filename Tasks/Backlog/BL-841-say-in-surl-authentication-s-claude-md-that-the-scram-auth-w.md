---
id: BL-841
title: Say in Surl.Authentication's CLAUDE.md that the SCRAM --auth words exist
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-841 — Say in Surl.Authentication's CLAUDE.md that the SCRAM --auth words exist

## Goal

`Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md`'s section "SASL `SCRAM-SHA-1` and
`SCRAM-SHA-256` (BL-416)" states the code as it is now: the `--auth` words exist, and it points to
ADR-0125.

## Context

- The section's first bullet says "`Surl.Console` has no `--auth` word for them yet, so no original
  server offers them". BL-575 (Done) added `scram-sha-256` and `scram-sha-1` to
  `OptionArgumentReader.AuthenticationMethodWords` in `Surl.Cli`, so `surl --auth scram-sha-256`
  makes SMTP, IMAP and POP3 offer them. The sentence is false (root `CLAUDE.md`, "Say what it does").
- BL-574 found it but could not fix it: `Foundation/Surl.Authentication.UnitLibrary` was in
  another Doing task's `touches` at the time.
- ADR-0125 records BL-416's SCRAM decisions.

## Acceptance criteria

- [ ] The section no longer says there is no `--auth` word for SCRAM; it says both are out of the
      default set and offered only when `--auth` names `scram-sha-256` or `scram-sha-1`.
- [ ] The section links ADR-0125.

## Notes

## Log

- 2026-11-05: Created.
