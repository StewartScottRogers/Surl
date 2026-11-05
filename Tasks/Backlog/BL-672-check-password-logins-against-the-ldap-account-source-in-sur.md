---
id: BL-672
title: Check password logins against the LDAP account source in Surl.Authentication
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-671]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-672 — Check password logins against the LDAP account source in Surl.Authentication

## Goal

`Surl.Authentication` checks the logins BL-671's ADR assigns to the LDAP account source against it, with the same
verdicts, notes and refusal delay as an account from `--user-file`.

## Context

- Decision: BL-671's ADR (which reading, which methods, timeouts, bounds, refusals). If the ADR places the code
  somewhere other than `Foundation/Surl.Authentication.UnitLibrary`, record it in Notes and stop, so the board can
  re-plan this task's `touches`.
- The directory is reached through an injected seam (no socket constructed here; only `Surl.Networking` constructs
  sockets), so tests replay the directory's answers in memory. Timeouts on the injected `TimeProvider`.
- ADR-0032 section 8: no difference a peer can see between an unknown user, a wrong password and a directory that
  does not answer; the one-second refusal delay applies.

## Acceptance criteria

- [ ] Named tests show: a password login accepted when the directory accepts it; refused when it refuses, with the
      same verdict and delay as an unknown `--user-file` account; a directory timeout refused the same way and
      noted; each method the ADR excludes never consults the directory; the directory's own bind password never
      appears in a note or exception message.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
