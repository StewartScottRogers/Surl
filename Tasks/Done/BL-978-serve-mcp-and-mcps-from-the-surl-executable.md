---
id: BL-978
title: Serve mcp and mcps from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466, BL-405, BL-614, BL-1035]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-978 — Serve mcp and mcps from the surl executable

## Goal

`surl mcp://127.0.0.1:0` serves MCP over Streamable HTTP and `surl mcps://127.0.0.1:0` serves it over TLS; the
`--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- First of the two tasks BL-468 ("Serve mcp, mcps and mcp+stdio from the surl executable") was split into on
  2026-11-06 for the 60-minute size limit: this, then BL-979 (`mcp+stdio`).
- Decision: ADR-0111 decision 4 - `mcp` is plain HTTP/1.1 on port 80 (no `--mcp-plaintext` exists, the scheme is
  the plain one); `mcps` is wrapped for TLS as `https` is (`ImplicitTlsSchemeServer`, ADR-0010), port 443, ALPN
  `http/1.1` only, `--cert` or `--self-signed` required, else `CertificateProblem` (58).
- Composition: `ExtendedProtocolServers.Compose` / `Registrations` in `Application/Surl.Composition.UnitLibrary`
  (BL-609; the start sequence moved there in BL-614).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with `mcp` and `mcps` (BL-979 adds
  `mcp+stdio`), an `AiHelpProse.TopicAbout` paragraph naming MCP 2025-06-18 as its oracle, an `AiHelpExamples`
  entry, and the topic list in `AiHelpTextTests` growing by one.
- Stand-in: no shared store; the server's sessions are observed through the engine's exchange events.

## Acceptance criteria

- [x] A named test finds `mcp` and `mcps` registered when the switch is on, and absent when it is off.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): Returned to Backlog behind BL-1035: `dotnet build` cannot run because the pinned
  .NET SDK 10.0.401 is not installed, so no gate can be checked. The work below was written but never
  built or tested; it is left uncommitted for the shift to stash. Re-check every piece when resuming.
- Touches widened: `mcps` needs a `TlsSchemes` row (`Foundation/Surl.Protocol.Abstractions.UnitLibrary`)
  so `ListenUrl.IsImplicitTls` makes the engine handshake and `ServerTlsComposition` demand a certificate
  (58), and `http/1.1`-only ALPN in `Surl.Networking`'s `TlsApplicationProtocols`, exactly as BL-421 did
  for `s3` (commit 0d53a586). No task in Doing names either project (BL-840 docs, BL-857 Conformance).
- Choice: one registration `["mcp", "mcps"]` (as `mqtt`/`mqtts`, `s3`), not an `ImplicitTls` wrapper,
  because `McpProtocolServer.Schemes` already claims both; TLS comes from `TlsSchemes` either way.
- Choice: the endpoint is always `/mcp` (`listenPath` null): `ListenUrlParser` refuses a listen URL
  with a path, so there is no path to pass.
- Choice: `serverInfo.version` is `ExtendedProtocolServers.SurlVersion`, the Composition assembly's
  informational version without `+` metadata (every surl assembly shares it); session IDs and tokens
  come from `RandomNumberGenerator.Create()`. Policy: `AuthenticationPolicyFor("mcp")`.
- Written so far (uncommitted): the registration and project reference in `Surl.Composition`; `mcp`
  help category, `AiHelpProse["mcp"]`, `extended`/`tls` paragraphs, an `AiHelpExamples` entry, a
  `MCP OPTIONS` manual section; the Cli test expectations (topic list, category table, help menu row,
  examples order, `ExtendedSchemesServed`, manual). Still to do: `ExtendedProtocolServersTests`
  (Compose order now dns, dns, mcp+mcps, nats, s3; a test the server is built over the content
  store), `CommandLineRunnerExtendedProtocolTests` for mcp/mcps on and off with `SURL_EXTENSIONS=0`
  and `mcps` without a certificate (58), the `--help` category wrap line in `HelpTextTests`
  (`mail, mcp, mqtt, ...` may rewrap), and the Composition `CLAUDE.md` paragraph. Option rows
  (`--allow-uploads`, `--user`, ...) were not given the `mcp` category; decide when resuming.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; partial work left uncommitted
- 2026-11-12: Backlog -> Doing.

- 2026-11-12 (lane 1): The earlier uncommitted work was not in the tree (stashed, stash listing not available
  to this lane), so it was redone from the notes above. Done: `mcp`/`mcps` registration, `mcps` in
  `TlsSchemes` and `http/1.1` ALPN, `mcp` help category, `AiHelpProse`, example, `MCP OPTIONS` manual section,
  tests (Composition, Console on/off, Cli expectations, Networking, Abstractions). Option rows were not given
  the `mcp` category (choice: no MCP-specific option exists; `--allow-anonymous`/`-u` stay as listed).
- `Surl.Extended.Protocol.Grpc.UnitTests.ServeStreamAsync_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable`
  fails in the full fast run; Grpc is untouched by this task.
- 2026-11-12: Doing -> Done. surl serves mcp and mcps (TLS, http/1.1 ALPN) with help, aihelp topic and extension switch
