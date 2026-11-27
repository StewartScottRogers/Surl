---
id: BL-1810
title: Say that proxy listen URLs may use the SOCKS schemes in ParseProxy's comment and the Surl.Cli CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary/ListenUrlParser.cs, Application/Surl.Cli.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1810 — Say that proxy listen URLs may use the SOCKS schemes in ParseProxy's comment and the Surl.Cli CLAUDE.md

## Goal

The XML comment on `ListenUrlParser.ParseProxy` and `Application/Surl.Cli.UnitLibrary/CLAUDE.md` name every scheme a proxy listen URL may use.

## Context

Found by BL-1806. Both say `http` or `https` only. `ProxyDefaultPorts.cs`, which `ParseProxy` reads, also accepts `socks4`, `socks4a`, `socks5` and `socks5h`, all with default port 1080 (ADR-0121 decision 5).

## Acceptance criteria

- [x] The XML comment on `ParseProxy` names all six schemes and their default ports as `ProxyDefaultPorts.cs` has them.
- [x] The `ParseProxy` sentence in `Application/Surl.Cli.UnitLibrary/CLAUDE.md` says the same.

## Notes

- Edited the two sentences in the session instead of delegating to `align-and-document`: a two-sentence change costs less than briefing an agent. No behaviour change.
- Default ports: 443 for `https`; 1080 for `http`, `socks4`, `socks4a`, `socks5`, `socks5h`.
- Verify: build 0 warnings 0 errors; `dotnet format --verify-no-changes` clean; fast tests 30134 passed, 0 failed, 3 skipped. Integration tests not run: comments and prose only.
- `SurlCommandLine.ProxyListenUrls`' comment has the same stale claim, outside `touches`: filed as BL-1814.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. ParseProxy's comment and the Surl.Cli CLAUDE.md name all six proxy listen URL schemes and their default ports
