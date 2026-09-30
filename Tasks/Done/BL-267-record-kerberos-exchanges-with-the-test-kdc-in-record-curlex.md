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
completed: 2026-09-30
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

- [x] `-KerberosTestKdc` starts `Run-KerberosTestKdc.cs`, waits for it to listen, runs curl,
      stops it, and writes `service.keytab` and `kdc.log` (one line per AS or TGS exchange:
      principal, enctype, error code) to `OutDirectory`.
- [x] It combines with the HTTP mode, with `-Smtp`, `-Imap`, `-Pop3` and `-SaslChallenge`, and
      with `-NoServer`; it is refused outside Windows with a message saying no pinned build there
      has Kerberos.
- [x] The script still refuses a binary not pinned in `UpstreamCurlBuilds.json`
      (`Assert-PinnedUpstreamCurl` unchanged), and its `.PARAMETER` help documents the new
      switches.
- [x] Without the BL-265 realm mapping, a run with `-KerberosTestKdc` still records (curl's
      failure is a fixture too) and `kdc.log` is empty.

## Notes

- Filed by BL-242 (ADR-0065).
- `Run-KerberosTestKdc.cs` (`dotnet run --file Run-KerberosTestKdc.cs -- --password <pw>
  --service <spn> ... [--user tester] [--port 88] [--keytab path] [--log path]`) references
  `Surl.Kerberos.TestKdc.UnitLibrary` and `Surl.Networking.UnitLibrary` (whose
  `SocketListenerFactory` binds the loopback listeners, as ADR-0065 decision 1 says; no project
  changed). The log comes from decorating the injected `IDatagramListener`/`IConnectionListener`
  so each request and answer is read back from DER; the KDC library needed no change.
- Log line format (my choice): `<AS|TGS> <client> <server> <ticket enctype or -> <error code>`,
  e.g. `AS tester@SURL.TEST krbtgt/SURL.TEST@SURL.TEST - 25`. A request the KDC cannot read logs `-`.
- Defaults (my choice): `-KerberosServicePrincipal` defaults to ADR-0065's four principals;
  `-KerberosPassword` has no default and is required, so the password curl's `-u` gives is
  always stated in the recording command. `-KerberosTestKdc` is allowed with every mode, not only
  the listed ones: refusing FTP or raw would add a rule without a reason.
- `dotnet run --file` is used, not `dotnet run <file>`: in a folder holding a `.csproj`, the
  latter runs the project instead.
- Verified 2026-09-30 on the lane machine (no BL-265 mapping): the runner answers a hand-made
  AS-REQ over UDP and TCP and logs `... - 25` for each (preauth required); HTTP Negotiate
  (curl exit 0, request without a token), `-Smtp -SaslChallenge 'GSSAPI=334 '` (curl exit 94)
  and `-NoServer` all record with `service.keytab` and an empty `kdc.log`; the password,
  principal-without-switch and unpinned-curl refusals fire. The success line's ticket enctype
  is read the way `KdcReply.ReadTicket` in the TestKdc tests reads it, but no AS-REP has been
  logged end to end yet: BL-268's first run shows it.
- The non-Windows refusal tests `[Environment]::OSVersion.Platform` (Windows PowerShell 5.1
  has no `$IsWindows`); it could not be run here.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Record-CurlExchange.ps1 -KerberosTestKdc runs the loopback test KDC through Run-KerberosTestKdc.cs and writes service.keytab and kdc.log beside the fixtures
