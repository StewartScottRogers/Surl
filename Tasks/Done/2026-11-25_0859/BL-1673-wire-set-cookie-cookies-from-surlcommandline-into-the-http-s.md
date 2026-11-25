---
id: BL-1673
title: Wire --set-cookie cookies from SurlCommandLine into the HTTP server in Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1661]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-24
---
# BL-1673 — Wire --set-cookie cookies from SurlCommandLine into the HTTP server in Surl.Composition

## Goal

`surl --set-cookie` makes the HTTP server set the cookie: `Surl.Composition` passes `SurlCommandLine.Cookies` to `HttpProtocolServer`.

## Context

ADR-0189 decision 7; BL-1661 reads the option into `SurlCommandLine.Cookies` (`CookieDefinition` list), BL-1660 added the `HttpProtocolServer` constructor taking the list. `ProtocolServerRegistrations.cs:46` still uses the constructor without cookies. Add `Cookies` to `ComposedProtocolServices` like `AdvertisesHttp3`. `Surl.Api` (`HttpServerOptions.Cookies`) and Surl.Mcp spellings are separate follow-ups.

## Acceptance criteria

- [x] A composition test starts the HTTP server with one cookie and sees its `Set-Cookie` line in a reply.
- [x] With no `--set-cookie` the reply is unchanged.
- [x] Build clean and fast tests green.

## Notes

- `ComposedProtocolServices.Cookies` is an init property defaulting to none, not a constructor parameter like `AdvertisesHttp3`: no existing caller changes and the default keeps today's behaviour ("configuration only grows"). The start sets it from `SurlCommandLine.Cookies`; `https` shares the `http` server, so it gets them too.
- Added the `Surl.Cookies` project reference to `Surl.Composition.UnitLibrary`.
- Tests: `SurlServerCompositionTests.Cookies.cs`. The cookie test failed before the change (reply had no `Set-Cookie`) and passes after.
- Stages: the change is a few lines, so plan, tests, implementation, review and documentation were done in this session with no subagent delegation. No upstream curl measurement: ADR-0189 holds it and BL-1662 is the live proof.
- Verification: `dotnet build -warnaserror` 0 warnings, 0 errors; fast tests 28881 passed, 0 failed, 3 skipped. `dotnet format` fixed older import ordering in five Composition files (`DnsConnectionRoutingProtocolServer.cs`, `ExtendedProtocolServers.cs`, `SurlServerComposition.Start.cs`, `ExtendedProtocolServersTests.cs`, `KafkaBrokerStoreTests.cs`). `dotnet format --verify-no-changes` still exits 2 on files this task did not change: filed as BL-1678.
- Follow-ups filed: BL-1674 and BL-1679 (HTTP/3 cookies), BL-1675 (`Surl.Api`), BL-1676 (`Surl.Mcp`), BL-1677 (FR-015 status), BL-1678.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. surl --set-cookie makes the http and https server set the cookie; composition tests and fast tests green
