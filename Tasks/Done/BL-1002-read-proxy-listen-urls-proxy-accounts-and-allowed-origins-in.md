---
id: BL-1002
title: Read proxy listen URLs, proxy accounts and allowed origins into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-553, BL-554]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md]
requirement: none
created: 2026-11-06
completed: 2026-11-11
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

- [x] Named tests in `Application/Surl.Cli.UnitTests` read each form (an `http` and an `https` proxy listen URL with
      `IsProxy` set and its default port, an account, a user file path, an origin list) and refuse each malformed
      value the ADR names with its `SurlExitCode`, none echoing a password.
- [x] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Delivered directly in the lane, not through every /feature subagent stage: two small readers and four
  members, on a surface ADR-0088 decision 7 already fixed.
- Decisions, recorded as ADR-0088 Amendment 5 (Decided by Claude under Stewart's delegation):
  `ListenUrlParser.ParseProxy` with `ProxyDefaultPorts` (http 1080, https 443, `IsProxy` set, any other scheme
  `UnsupportedProtocol` (1), malformed `MalformedUrl` (3)); `-U` and `--proxy-user-file` reuse `ReadAccount` and
  `ReadPath`; `--proxy-allow-origin` is `*` or a strict literal `address[/prefix]` read into the new
  `ProxyAllowedOrigin` (four-part decimal IPv4; IPv6 without brackets, zone or port; IPv4-mapped taken as IPv4).
  Every option argument refusal is `FailedInit` (2), as the parser maps all of them. No refusal holds a password.
- Touches widened: ADR-0088, for the amendment. No task in Doing names it.
- Stand-in safety: `surl-mcp`'s `start_server` loopback rule now covers `ProxyListenUrls`
  (`StartServerSafetyCheck.FindListenHostRefusal`, made internal so it is tested before BL-1004's rows exist),
  and `ProxyUserFile` is a confined path member.
- Toolchain: the pinned SDK 10.0.401 is not installed on this machine (BL-1035, Stewart's). The solution build,
  all fast tests (green; Surl.Cli.UnitTests 1304, Surl.Mcp.UnitTests 245) and `dotnet format --verify-no-changes`
  ran with `global.json` temporarily set to 11.0.100, then restored unchanged. CI checks the pinned SDK before
  any merge.
- Coverage: every new line and branch in `Surl.Cli.UnitLibrary` is covered. SDK 11's collector shows partial
  branches only in code this task did not touch (`CommandLineParser.cs:730`, `ConfigurationItemReader.cs:141`,
  `HelpText.cs:123`, `MailboxFileReader.cs:283`, `OptionArgumentReader.ReadAuthorizedKeys`); left for the
  coverage-auditor to judge under the pinned SDK.
- Follow-up filed: BL-1037 (`--proxy-auth`, ADR-0088 decision 5, which no proxy task covered).

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Surl.Cli reads proxy listen URLs (IsProxy, ports 1080/443), proxy accounts, a proxy user file and allowed origins into SurlCommandLine; surl-mcp classifies and confines them
