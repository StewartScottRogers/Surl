---
id: BL-345
title: Record BL-312's OpenLDAP measurements in ADR-0076
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-312]
touches: [Documentation/Planning/Decisions]
requirement: FR-052
created: 2026-09-30
completed:
---
# BL-345 — Record BL-312's OpenLDAP measurements in ADR-0076

## Goal

ADR-0076 carries an "Amended" or "Measured against surl" section stating what BL-312's
`UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests` measured with the pinned OpenLDAP build
against a live surl, so the ADR's case table is true of the cases it did not measure.

## Context

- BL-312 could not edit `Documentation/Planning/Decisions` (BL-327 held it), so its results live in
  its task file's Notes and in the test class.
- What to record (all measured 2026-09-30 in a `mcr.microsoft.com/dotnet/sdk:10.0` Linux container
  with the pinned build, SHA-256 `8D4572E8...2398`, at its pin path):
  - `--ssl-reqd` against a StartTLS certificate curl does not trust: exit **64** `SSL certificate
    OpenSSL verify result: self-signed certificate (18)`, not `ldaps`'s 60.
  - Every entry ends with one more `\n` than `WinLDAP`'s output, also an entry with no selected
    attribute (`DN: ou=many,dc=example,dc=com\n\n`).
  - A simple bind refused with surl's 13 or 48 (no `-u`, no `--allow-anonymous`): exit 38
    `LDAP: cannot bind`.
  - `ldaps` with `--cacert` naming a CA that signed surl's `--cert`: exit 0; SASL `EXTERNAL` over
    `ldaps` with a client certificate surl's `--cacert` verifies: exit 0, and without one: 67.
  - `AUTH=*` against `--auth digest-md5,cram-md5,plain`: exit 0.
- Also update ADR-0072 decision 5's last bullet ("BL-312 proves `ldaps` through the OpenLDAP build")
  to say it was proved, if BL-344 has not.

## Acceptance criteria

- [ ] ADR-0076 states each measurement above with the build, its SHA-256, the arguments and date.
- [ ] `Documentation/Planning/Decisions/README.md` still indexes it; the fast tests are green.

## Notes

- Filed by BL-312's run (lanes may file follow-up tasks, Stewart 2026-09-30).

## Log

- 2026-09-30: Created.
