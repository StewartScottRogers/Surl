---
id: BL-1390
title: Decide and align whether --auth negotiate accepts a raw NTLM LDAP bind with what --aihelp ldap says
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests, Application/Surl.Cli.UnitLibrary/AiHelpProse.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1390 — Decide and align whether --auth negotiate accepts a raw NTLM LDAP bind with what --aihelp ldap says

## Goal

What `surl --aihelp ldap` says about NTLM binds under `--auth negotiate` matches what the LDAP server does, and the
choice is recorded in an ADR.

## Context

- Surly cookbook bug report 2026-11-18 20:25, finding 2 (commit `a0236e175`). The report's client was partly the
  Curl port, which is never evidence: re-measure with the pinned upstream curl Windows build through
  `Record-CurlExchange.ps1`.
- The ldap topic in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` (near line 302) says: "The Windows curl's other
  binds, from its NTLM, Negotiate and Digest options, are NTLM (accepted with `--auth ntlm` or `--auth negotiate`)".
  Observed: a `curl --ntlm` bind is accepted under `--auth ntlm` but refused under `--auth negotiate`; a
  `curl --negotiate` bind is accepted under `negotiate`.
- Two fixes are possible: accept a raw NTLM bind under `negotiate` (as Windows Negotiate may fall back to NTLM), or
  correct the help to say `--ntlm` binds need `--auth ntlm` and Negotiate binds carrying NTLM need `--auth negotiate`.

## Acceptance criteria

- [x] An ADR in `Documentation/Planning/Decisions` ("Decided by Claude under Stewart's delegation") picks one, citing
      what pinned upstream curl on Windows sends for `--ntlm` and `--negotiate` LDAP binds (bytes measured) and how
      Surl's HTTP server treats a raw NTLM `Authorization` under `--auth negotiate` today, for consistency.
- [x] The LDAP server and `--aihelp ldap` agree with the ADR; a named fast test in `Surl.Protocol.Ldap.UnitTests`
      pins a raw NTLM bind under `--auth negotiate` (accepted, or refused with the result code the ADR names).
- [x] `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` stays green.
- [x] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- Decided (ADR-0178): option B - raw NTLM needs `--auth ntlm`, Negotiate binds need `--auth negotiate`; matches HTTP and keeps secure-by-default. Help corrected, no production change. Bytes cited from existing pinned-curl fixtures (ldap-ntlm-sealed, ldap-negotiate-sealed), not re-recorded. The refusal itself lives in Surl.Authentication (unchanged), so the LDAP test pins the server answering authMethodNotSupported (7) when the policy refuses NTLM.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. ADR-0178: raw NTLM LDAP bind needs --auth ntlm; --aihelp ldap corrected and a Sicily NTLM refusal test added
