---
id: BL-1176
title: Add SurlServerOptions.ControlListenUrl and ControlTokenFile and report SurlServer.ControlUrl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-065
created: 2026-11-14
completed:
---
# BL-1176 — Add SurlServerOptions.ControlListenUrl and ControlTokenFile and report SurlServer.ControlUrl

## Goal

A .NET test harness that starts Surl in process can also serve the loopback control endpoint, for a non-.NET part of the same suite, by setting `SurlServerOptions.ControlListenUrl` and `ControlTokenFile`, and reads the bound URL from `SurlServer.ControlUrl`.

## Context

- Stand-in audit 2026-11-14: `Documentation/Product/Requirements.md` FR-065 says "`Surl.Api` has no control-endpoint option", although ADR-0124 decision 7 meant the endpoint for "surl and Surl.Api" (BL-816's title). `Application/Surl.Composition.UnitLibrary/ComposedServing.cs` already serves it when the command line has `--control` (`ServeWithControlAsync`) and exposes `ControlListenUrl` once bound.
- ADR-0099 decision 4: a `SurlServerOptions` property is a spelling of a `surl` option, turned into words in `Application/Surl.Api/SurlServerArguments.cs` and parsed by `Surl.Cli`, so refusals are `surl`'s own: `--control must listen on loopback ...`, `--control needs http, not <scheme>`, `--control needs --control-token-file`, `--control-token-file without --control` (ADR-0124 decision 7), surfacing as `SurlServerStartException`. The names in the title are this task's, following that rule: `string? ControlListenUrl`, `string? ControlTokenFile`, and `string? SurlServer.ControlUrl` (the bound URL, null without a control endpoint).
- Defaults keep today's behaviour: both null, no endpoint.

## Acceptance criteria

- [ ] `SurlServerOptions.ControlListenUrl` and `ControlTokenFile` exist with XML doc comments naming `--control` and `--control-token-file`, and `SurlServerArguments` spells them as those options.
- [ ] Named tests in `Application/Surl.Api.UnitTests`: with `http://127.0.0.1:0` and a valid token file, `SurlServer.ControlUrl` is the bound URL with a non-zero port, and an HTTP request to `<ControlUrl>/v1/events` with the token gets `200`; without the options `ControlUrl` is null.
- [ ] Named tests: each of the four refusals above throws `SurlServerStartException` carrying `surl`'s message.
- [ ] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
