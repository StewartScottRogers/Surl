---
id: BL-1002
title: Read proxy listen URLs, proxy accounts and allowed origins into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-553, BL-554]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1002 — Read proxy listen URLs, proxy accounts and allowed origins into the Surl.Cli configuration model

## Goal

`Surl.Cli` can read what ADR-0088 decision 7's proxy options carry - a proxy listen URL (`http://` or `https://`,
with `ListenUrl.IsProxy = true`), a proxy account (`user:password`), a proxy user file and an allowed-origin list -
into `SurlCommandLine` members, each read and refused as the ADR says, fully unit tested. No option row uses the
readers yet; BL-1004 adds the rows.

## Context

- First of the three tasks BL-555 ("Serve the HTTP and HTTPS proxies from the surl executable") was split into on
  2026-11-06 for the 60-minute size limit, in the order BL-653's split proved: this (model and readers), BL-1003
  (composition), BL-1004 (option rows, help, manual, `--aihelp` topic and the `CommandLineRunner` tests).
- Decision: ADR-0088 (BL-550) decisions 2 (allowed origins), 5 (accounts), 6 (TLS) and 7 (command line: `-x`/
  `--proxy`, `-U`/`--proxy-user`, `--proxy-user-file`, `--proxy-allow-origin`, and limits). The ADR wins over this
  summary.
- BL-554 (ADR-0088 Amendment 2): every listen URL built from `--proxy` must set `ListenUrl.IsProxy = true`.
  Code: `ListenUrlParser`, `SchemeDefaultPorts`, and the `--user`/`--user-file` readers to reuse for the accounts.
  A proxy password is a secret: no refusal or `ToString()` echoes it.
- Adding `SurlCommandLine` members makes `Surl.Mcp.UnitTests`'
  `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` fail until each is classified in
  `Application/Surl.Mcp.UnitLibrary` - a one-line entry per member.
- The SOCKS task BL-560 reuses these members for SOCKS (ADR-0121 decision 4).
- Stand-in: none in this task; it reads configuration only.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` read each form (an `http` and an `https` proxy listen URL with
      `IsProxy` set and its default port, an account, a user file path, an origin list) and refuse each malformed
      value the ADR names with its `SurlExitCode`, none echoing a password.
- [ ] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
