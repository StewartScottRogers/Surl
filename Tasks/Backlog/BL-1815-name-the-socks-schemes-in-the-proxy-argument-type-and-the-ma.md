---
id: BL-1815
title: Name the SOCKS schemes in the --proxy argument type and the manual's PROXY OPTIONS section
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-26
completed:
---
# BL-1815 — Name the SOCKS schemes in the --proxy argument type and the manual's PROXY OPTIONS section

## Goal

The help text surl writes about `-x`/`--proxy` names every scheme and default port a proxy listen URL may use.

## Context

Found by BL-1814. `ListenUrlParser.ParseProxy` accepts `http`, `https`, `socks4`, `socks4a`, `socks5` and
`socks5h`; `ProxyDefaultPorts` gives 443 for `https` and 1080 for the other five (ADR-0121 decision 5).
Two texts in `Application/Surl.Cli.UnitLibrary` name only `http` and `https`:

- `OptionArgumentReader.ProxyListenUrlText`: its argument type reads "an http or https listen URL, as a bare
  argument takes it; default port 1080 for http and 443 for https".
- `ManualText.Text`, section PROXY OPTIONS: `-x` "makes one listener an HTTP proxy (http://) or an HTTPS proxy
  (https://...)" and lists default ports for those two only.

## Acceptance criteria

- [ ] The allowed-values text of `OptionArgumentReader.ProxyListenUrlText` names all six schemes, with 443 as the default port for `https` and 1080 for the other five.
- [ ] The manual's PROXY OPTIONS section says that `-x`/`--proxy` also takes `socks4`, `socks4a`, `socks5` and `socks5h` listen URLs, with default port 1080, and every manual line stays within 79 columns.
- [ ] A test in `Surl.Cli.UnitTests` fails when a scheme in `ProxyDefaultPorts` is missing from the allowed-values text of `ProxyListenUrlText`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
