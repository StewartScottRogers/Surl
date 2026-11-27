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
completed:
---
# BL-1814 — Say that proxy listen URLs may use the SOCKS schemes in the SurlCommandLine.ProxyListenUrls comment

## Goal

The XML comment on `SurlCommandLine.ProxyListenUrls` names every scheme a proxy listen URL may use.

## Context

Found by BL-1810. The comment says `http` or `https` only. `ListenUrlParser.ParseProxy` also accepts `socks4`, `socks4a`, `socks5` and `socks5h`, as `ProxyDefaultPorts.cs` lists them (ADR-0121 decision 5).

## Acceptance criteria

- [ ] The XML comment on `SurlCommandLine.ProxyListenUrls` names all six schemes, or refers to `ProxyDefaultPorts` for the list without saying `http` or `https` only.

## Notes

## Log

- 2026-11-26: Created.
