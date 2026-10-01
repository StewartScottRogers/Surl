---
id: BL-350
title: Say in the ldap --aihelp topic that --keytab answers a Kerberos Negotiate bind
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-049
created: 2026-10-01
completed:
---
# BL-350 — Say in the ldap --aihelp topic that --keytab answers a Kerberos Negotiate bind

## Goal

`surl --aihelp ldap` says that with `--keytab` (and `--auth negotiate`) a Windows curl
`--negotiate` bind to `ldap://localhost:<port>` is answered with Kerberos inside `GSS-SPNEGO`
and sealed with RFC 4121 wrap tokens, and that without `--keytab` it is answered with NTLM.

## Context

- ADR-0072 Amendment 1 (BL-327) decides it; BL-346's
  `UpstreamCurlBindsToSurlOverLdapWithKerberosTests` proves it against the pinned Windows build.
- `Surl.Cli.UnitLibrary/AiHelpProse.cs`, the `ldap` topic paragraph that begins "Every search but
  the root DSE's needs a successful bind": today it says the Negotiate bind is NTLM only.
- The service principal `WinLDAP` asks for is `ldap/<the machine's host name>:<port>`, with the
  port; the keytab must hold it, and the account named by the ticket's principal must be in
  `--user-file` (or `--allow-anonymous` given).

## Acceptance criteria

- [ ] The `ldap` topic of `surl --aihelp ldap` names `--keytab`, Kerberos and the
      `ldap/<host>:<port>` service principal, and still says NTLM answers without `--keytab`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Filed by BL-346, which found the topic silent on Kerberos.

## Log

- 2026-10-01: Created.
