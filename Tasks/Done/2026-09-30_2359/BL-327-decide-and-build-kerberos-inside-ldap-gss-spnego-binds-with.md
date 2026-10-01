---
id: BL-327
title: Decide and build Kerberos inside LDAP GSS-SPNEGO binds with an RFC 4121 security layer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-329]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Kerberos.UnitLibrary, Surl.Kerberos.UnitTests, Documentation/Planning/Decisions, Record-CurlExchange.ps1, Run-KerberosAcceptor.cs]
requirement: FR-049
created: 2026-09-30
completed: 2026-09-30
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

- [x] An amendment to ADR-0072 records the measured `WinLDAP` Kerberos bind (or why it cannot be
      reached) and decides the answer.
- [x] If reachable: tests replay the recorded AP-REQ and first wrapped buffer and unwrap it to the
      plain search request; `dotnet build -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every touched library.

## Notes

- Filed by BL-284 (ADR-0072).
- Reachable with no machine change: `ldap://localhost:<port>` makes `WinLDAP` ask the KDC for
  `ldap/<machine host name>:<port>@SURL.TEST` (the canonical host name, with the port, in the
  user's realm), not `ldap/localhost`. Measured 2026-09-30, recorded as
  `Surl.Authentication.UnitTests/Fixtures/ldap-kerberos-sealed`: optimistic AP-REQ in a
  `NegTokenInit` (MS-KRB5, Kerberos, NEGOEX, NTLMSSP), mutual and confidentiality asked, no
  `mechListMIC`; one-leg `accept-completed` with the AP-REP accepted; no RFC 4752 layer
  negotiation; sealed wrap tokens `EC` 0 `RRC` 28 both ways; curl exited 0 with the entry.
- Touches widened (no task in Doing names them): `Record-CurlExchange.ps1`, to answer the bind live
  (`-LdapKerberosAcceptor`) and to fix a hang - the KDC child inherited the listening socket, so
  `-Ldap -KerberosTestKdc` never finished - by starting it before the listener; and the new
  `Run-KerberosAcceptor.cs` it drives, a file-based app over `Surl.Kerberos`, as
  `Run-KerberosTestKdc.cs` is for the KDC.
- Defaults taken: `RRC` 28 on surl's sealed tokens (Windows' own, measured to work; RFC 4121 obliges
  receivers to accept any); `--allow-anonymous` accepts an unchecked Kerberos bind, since the
  layer's keys come from the ticket, not a password; no layer when the client asks for neither
  confidentiality nor integrity. All recorded in ADR-0072 Amendment 1.
- Follow-up filed: BL-346, the end-to-end proof against a live `surl ldap --keytab` once BL-310 and
  BL-311 land.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. WinLDAP's Kerberos GSS-SPNEGO bind (measured for ldap://localhost) is accepted with --keytab in one leg and sealed with RFC 4121 wrap tokens (ADR-0072 Amendment 1)
