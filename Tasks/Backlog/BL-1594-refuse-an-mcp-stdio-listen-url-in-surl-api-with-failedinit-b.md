---
id: BL-1594
title: Refuse an mcp+stdio listen URL in Surl.Api with FailedInit before anything binds
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1594 — Refuse an mcp+stdio listen URL in Surl.Api with FailedInit before anything binds

## Goal

`SurlServer.StartAsync` with the listen URL `mcp+stdio:` (alone or beside others) throws
`SurlServerStartException` with `SurlExitCode.FailedInit` (2) and a line naming why, before any
listener binds, instead of `UriFormatException` with a listener left bound.

## Context

Surly cookbook bug report 2026-11-22 (`Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1116-Surl-bug-report.md`,
item 1), against Surl `5da5b3eaf`; nothing in the code paths below changed since.

The cause, in today's code:
- `Application/Surl.Cli.UnitLibrary/ListenUrlParser.cs:133-145` (`ParseStdio`) accepts `mcp+stdio:` whenever the
  extended schemes are on, as a `ListenUrl("mcp+stdio", "", 0)`.
- Only the command line serves it: `Application/Surl.Console/CommandLineRunner.cs:262-264` sends it to
  `McpStdioComposition.ServeAsync`. `Surl.Api` never asks `McpStdioComposition.IsAsked`, so
  `Application/Surl.Api/SurlServer.cs:325` (`SurlServerComposition.ComposeAsync`) composes it like any listen URL;
  `ExtendedProtocolServers.cs:145` registers `McpStdioListenUrlServer`, which only claims the scheme.
- The serving engine binds a listener for host `""`: in production `ListenAddressResolver.ResolveAsync`
  (`Foundation/Surl.Networking.UnitLibrary/ListenAddressResolver.cs:41-46`) resolves `""` with
  `Dns.GetHostAddressesAsync`, which yields the machine's own addresses, not loopback.
- Once bound, `SurlServer.cs:380` calls `ToUri` (`SurlServer.cs:418`), which builds `mcp+stdio://:<port>/` and
  `new Uri(...)` throws `UriFormatException`. The started `ComposedServing` is never stopped or disposed.

Decision (by the standing rules; ADR-0162 decision 1 already says no host that starts servers can reach a session
on a process's own standard input and output, and points a test at `mcp:`/`mcps:` instead): `Surl.Api` refuses
`mcp+stdio:` as a start failure, `FailedInit`, the exit code `surl` gives every other `mcp+stdio:` refusal
(`Application/Surl.Composition.UnitLibrary/McpStdioComposition.cs:31-51`). A `SurlServerStartException` rather
than an `ArgumentException` because the word is one `surl` accepts and `surl-mcp`'s `start_server` reports a start
failure with its exit code (`StartServerTool.cs:122-150`).

Where: in `SurlServer.StartAsync(SurlServerOptions, SurlServerHostSeams, CancellationToken)`
(`SurlServer.cs:307-336`), right after `SurlServerArguments.Parse`, refuse when
`McpStdioComposition.IsAsked(commandLine)`, with the one line
`mcp+stdio: serves the standard input and output of a surl process and cannot be started in-process; listen on mcp://127.0.0.1:0/ instead`
(keep the text in one constant). With the extended schemes off, the parser already refuses it with
`UnsupportedProtocol` (1); keep that.

Stand-in: no store, event or fault changes. A test that wants an MCP server it can observe and fault uses
`mcp://127.0.0.1:0/` (ADR-0162 decision 1); the refusal text says so.

## Acceptance criteria

- [ ] `SurlServerTests.StartAsync_McpStdioListenUrl_ThrowsFailedInitBeforeAnythingBinds` (in
      `Application/Surl.Api.UnitTests`, `AreExtensionsTurnedOn = true`, `ApiTestSeams`) passes: the exception is
      `SurlServerStartException`, `ExitCode` is `SurlExitCode.FailedInit`, `Lines` is the one line above, and the
      `FakeListenerFactory` started no listener. It fails on today's code (`UriFormatException`).
- [ ] `SurlServerTests.StartAsync_McpStdioBesideHttp_ThrowsFailedInitBeforeAnythingBinds` passes the same way for
      `ListenUrls = { "http://127.0.0.1:0", "mcp+stdio:" }`.
- [ ] `SurlServerTests.StartAsync_McpStdioWithExtensionsOff_ThrowsUnsupportedProtocol` passes (exit 1, the parser's line).
- [ ] `Application/Surl.Api/CLAUDE.md` ("What it is") and `Application/Surl.Api/README.md` state that `mcp+stdio:`
      is refused with `FailedInit` and why.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean and
      `dotnet test Application/Surl.Api.UnitTests --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration`; `Surl.Api` keeps 100% line and branch coverage.

## Notes

`surl-mcp`'s `start_server` goes through this same `SurlServer.StartAsync` (`RunningSurlServers.cs:24-25`), so this
fix is what turns its JSON-RPC -32603 into a tool error; the test proving that is a separate task that depends on
this one.

## Log

- 2026-11-22: Created.
