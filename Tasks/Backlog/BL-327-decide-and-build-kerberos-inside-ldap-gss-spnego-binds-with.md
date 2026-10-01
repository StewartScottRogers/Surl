---
id: BL-327
title: Decide and build Kerberos inside LDAP GSS-SPNEGO binds with an RFC 4121 security layer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-325]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Kerberos.UnitLibrary, Surl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-327 — Decide and build Kerberos inside LDAP GSS-SPNEGO binds with an RFC 4121 security layer

## Goal

An LDAP `GSS-SPNEGO` bind whose SPNEGO token selects Kerberos is accepted with a keytab
(`--keytab`) and protected afterwards with RFC 4121 wrap tokens, as an amendment to ADR-0072
records from measurement.

## Context

- ADR-0072 decision 4 answers Kerberos inside `GSS-SPNEGO` by ADR-0040's rule (NTLM selected),
  because the pinned `WinLDAP` build never asks the KDC for `ldap://127.0.0.1` (measured with
  ADR-0065's test KDC) and resolves the URL's host itself, ignoring `--resolve`.
- Measuring needs a host name that the machine's own resolution maps to 127.0.0.1 and the
  `SURL.TEST` realm (BL-265's mapping); a hosts-file entry is a machine change, so find a way that
  needs none (e.g. a name Windows already resolves to loopback, such as `localhost`, with the
  matching `ldap/localhost` principal) before asking for one.
- ADR-0057 and ADR-0064 (Surl's Kerberos and Negotiate); RFC 4121 section 4.2 (wrap tokens),
  RFC 4752 section 3.3 for the layer negotiation as Active Directory applies it to `GSS-SPNEGO`.

## Acceptance criteria

- [ ] An amendment to ADR-0072 records the measured `WinLDAP` Kerberos bind (or why it cannot be
      reached) and decides the answer.
- [ ] If reachable: tests replay the recorded AP-REQ and first wrapped buffer and unwrap it to the
      plain search request; `dotnet build -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every touched library.

## Notes

- Filed by BL-284 (ADR-0072).

## Log

- 2026-09-30: Created.
