---
id: BL-282
title: Approve obtaining an upstream curl 8.21.0 build whose LDAP runs over OpenLDAP
priority: Normal
assignee: Stewart
pipeline: direct
depends-on: []
touches: []
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-282 — Approve obtaining an upstream curl 8.21.0 build whose LDAP runs over OpenLDAP

## Goal

Stewart says yes or no to obtaining one more upstream curl 8.21.0 build - one whose `ldap` and
`ldaps` run over OpenLDAP (upstream's `lib/openldap.c`) - so the LDAP server can be proved against
the half of upstream curl's LDAP that no pinned build exercises today.

## Context

- Measured from `UpstreamCurlBuilds.json` (pinned 2026-09-28 and 2026-09-29): only the two Windows
  builds that report `WinLDAP` (the Git for Windows reference build and curl.se's 8.22.0
  supplementary build) list `ldap ldaps`. The static-curl builds - the Linux and macOS reference
  builds and ADR-0030's Windows SMB build - do not list LDAP at all.
- Upstream curl has two LDAP implementations (tag `curl-8_21_0`, checked 2026-09-30):
  - `lib/ldap.c` over Windows' `wldap32` (what the pinned Windows builds use): a simple bind, or
    with `--ntlm`, `--negotiate` or `--digest` an `ldap_bind_s` with `LDAP_AUTH_NTLM`,
    `LDAP_AUTH_NEGOTIATE` or `LDAP_AUTH_DIGEST`, then `ldap_search_s`.
  - `lib/openldap.c` over OpenLDAP's `libldap` (what curl on Linux and macOS normally uses): a
    `STARTTLS` extended operation for `--ssl`/`--ssl-reqd`, a search of the root DSE for
    `supportedSASLMechanisms`, and SASL binds through curl's own SASL code (`--login-options
    AUTH=...`, `--sasl-ir`, `--oauth2-bearer`), then an asynchronous `ldap_search_ext`.
- So without such a build, LDAP `STARTTLS`, LDAP SASL binds and every byte `lib/openldap.c`
  sends are unmeasured, and Surl's LDAP server could not be called a faithful mate for them
  (ADR-0003: upstream curl is the only oracle; root `CLAUDE.md`: downloading a build to pin needs
  Stewart's approval).
- What the approval would cover, as ADR-0016 and ADR-0030 did for theirs: obtaining a build of
  unpatched tag `curl-8_21_0` with OpenLDAP, for Linux (and macOS if one can be had), by download
  of a third-party static build that includes OpenLDAP or by building the tag from source in CI -
  which one, where it lives, and how CI obtains it are decided afterwards by ADR in BL-287.
- Nothing is blocked meanwhile: the LDAP server is built and proved against the Windows `WinLDAP`
  reference build first; only BL-287 and the OpenLDAP proof after it wait on this answer.

## Acceptance criteria

- [ ] Stewart's answer (yes, or no with a reason) is recorded in this task's `Log` when it moves to
      `Done`.

## Notes

## Log

- 2026-09-30: Created.
