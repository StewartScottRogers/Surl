---
id: BL-267
title: Record Kerberos exchanges with the test KDC in Record-CurlExchange.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-266]
touches: [Record-CurlExchange.ps1, Run-KerberosTestKdc.cs]
requirement: FR-046
created: 2026-09-30
completed:
---
# BL-267 — Record Kerberos exchanges with the test KDC in Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -KerberosTestKdc` runs the hand-built test KDC while pinned upstream curl
runs, and leaves `service.keytab` and `kdc.log` beside the other fixtures.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md` decision 4.
- `Run-KerberosTestKdc.cs`: a C# file-based app at the repository root (`dotnet run
  Run-KerberosTestKdc.cs`) referencing `Surl.Kerberos.TestKdc.UnitLibrary`; it takes the realm
  (`SURL.TEST`), the user's password and the service principals, writes the keytab, says when it
  is listening on `127.0.0.1:88` (UDP and TCP), and serves until its standard input closes.
- New parameters: `-KerberosTestKdc`, `-KerberosPassword`, `-KerberosServicePrincipal` (repeatable).

## Acceptance criteria

- [ ] `-KerberosTestKdc` starts `Run-KerberosTestKdc.cs`, waits for it to listen, runs curl,
      stops it, and writes `service.keytab` and `kdc.log` (one line per AS or TGS exchange:
      principal, enctype, error code) to `OutDirectory`.
- [ ] It combines with the HTTP mode, with `-Smtp`, `-Imap`, `-Pop3` and `-SaslChallenge`, and
      with `-NoServer`; it is refused outside Windows with a message saying no pinned build there
      has Kerberos.
- [ ] The script still refuses a binary not pinned in `UpstreamCurlBuilds.json`
      (`Assert-PinnedUpstreamCurl` unchanged), and its `.PARAMETER` help documents the new
      switches.
- [ ] Without the BL-265 realm mapping, a run with `-KerberosTestKdc` still records (curl's
      failure is a fixture too) and `kdc.log` is empty.

## Notes

- Filed by BL-242 (ADR-0065).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
