---
id: BL-1814
title: Say that proxy listen URLs may use the SOCKS schemes in the SurlCommandLine.ProxyListenUrls comment
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary/SurlCommandLine.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1814 — Say that proxy listen URLs may use the SOCKS schemes in the SurlCommandLine.ProxyListenUrls comment

## Goal

The XML comment on `SurlCommandLine.ProxyListenUrls` names every scheme a proxy listen URL may use.

## Context

Found by BL-1810. The comment says `http` or `https` only. `ListenUrlParser.ParseProxy` also accepts `socks4`, `socks4a`, `socks5` and `socks5h`, as `ProxyDefaultPorts.cs` lists them (ADR-0121 decision 5).

## Acceptance criteria

- [x] The XML comment on `SurlCommandLine.ProxyListenUrls` names all six schemes, or refers to `ProxyDefaultPorts` for the list without saying `http` or `https` only.

## Notes

- The comment now names all six schemes, refers to `ProxyDefaultPorts` and cites ADR-0121 decision 5. No behaviour change, so no new ADR.
- Made directly in the session, without the `align-and-document` agent: a one-comment edit does not repay an agent's cost.
- Verify (2026-11-26): `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` exit 0; fast tests 30134 passed, 0 failed, 3 skipped. Integration tests not run: only a comment changed.
- Filed BL-1815: the `--proxy` argument type text and the manual's PROXY OPTIONS section also name only `http` and `https`; both are outside this task's `touches`.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The SurlCommandLine.ProxyListenUrls comment names all six proxy listen URL schemes
