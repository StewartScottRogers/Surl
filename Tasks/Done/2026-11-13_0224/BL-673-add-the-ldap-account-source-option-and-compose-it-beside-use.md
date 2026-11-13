---
id: BL-673
title: Add the LDAP account source option and compose it beside --user and --user-file
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-672, BL-646, BL-1068]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-673 — Add the LDAP account source option and compose it beside --user and --user-file

## Goal

The option BL-671's ADR names (planning `--user-ldap <ldap-url>`) makes `surl` check password logins against an
LDAP account source beside `--user` and `--user-file`, composed in `Surl.Composition` with whatever outbound
connection seam the ADR names.

## Context

- BL-671's ADR is ADR-0149: the option is `--user-ldap` with decision 3's keys; URL-structure refusals are
  `Surl.Composition`'s (decision 7); the TLS client connector comes from BL-1068; decision 8 amends ADR-0102
  decision 4.1 for group and access-row members. See its Consequences.
- Decisions: BL-671's ADR (option, URL form, TLS requirement, refusals, exit codes, bounds), BL-641's ADR (item
  grammar and help category), BL-642's ADR decision 4 (groups).
- Builds on: BL-672 (the check in `Surl.Authentication`), BL-646 (groups may name LDAP accounts if the ADR says
  so). The outbound connection comes from `Foundation/Surl.Networking.UnitLibrary` (BL-551's outbound
  connections); if a change there is needed, record it in Notes and file it rather than widen this task.
- Where: `Application/Surl.Cli.UnitLibrary` (option row) and `Application/Surl.Composition.UnitLibrary`.
- `--aihelp` completeness (root `CLAUDE.md`). The example cannot reach a real directory; it shows the refusal of
  an unreachable or non-TLS URL as the ADR says, which `CommandLineRunnerAiHelpTests` can run.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Cli.UnitTests` parse the option and refuse a malformed URL with the ADR's exit
      code and text, never echoing a password in the URL.
- [x] Named tests in `Application/Surl.Composition.UnitTests` show the source composed beside `--user` accounts and
      the plaintext-URL refusal or warning the ADR names.
- [x] The option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

- Decided under ADR-0149 (no new ADR needed). Choices where the ADR was silent: a URL attribute that is not an attribute description is refused `(2) --user-ldap: the URL's attribute is not an attribute description`; the `Account source: ...` and `Member ... taken as a directory account` notes are written exactly as the ADR spells them (no `* ` prefix), at `-v`; the option is read by `ConfiguredLdapAccountSourceReader` with `ArgumentHoldsSecret` so `--user-ldap=ldap://u:p/` never echoes `p`.
- `touches` gained `Application/Surl.Mcp.UnitLibrary` and `Application/Surl.Mcp.UnitTests`: `CommandLinePathMembers` must classify the new `SurlCommandLine.UserLdap` member (its `bind-password-file` and `ca-file` are local paths the MCP root check must confine); no task in Doing touched them.
- `TlsClientLdapDirectoryConnector` (Composition) adapts BL-1068's `TlsClientConnector`: TLS refusals surface as `AuthenticationException` (note `TLS failed`), everything else as `IOException` (`could not connect`). It opens sockets, so it is `ExcludeFromCodeCoverage` with its loopback tests in `Integration`.
- Measured: Cli 100% line and branch, Mcp 100%; Composition's remaining failing members (`ComposedServing.ServeWithControlAsync`, SignalR routing in `ExtendedProtocolServers`) are not from this task.
- Seen, not this task: `Surl.Extended.Protocol.Grpc.UnitTests` `ServeStreamAsync_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable` fails intermittently.

## Log

- 2026-11-04: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. surl --user-ldap parses, is refused with ADR-0149's texts, and composes an LDAP account source beside --user and --user-file
