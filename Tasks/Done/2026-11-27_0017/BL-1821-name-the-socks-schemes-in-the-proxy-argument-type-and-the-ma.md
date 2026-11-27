---
id: BL-1821
title: Name the SOCKS schemes in the --proxy argument type and the manual's PROXY OPTIONS section
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1821 — Name the SOCKS schemes in the --proxy argument type and the manual's PROXY OPTIONS section

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

- [x] The allowed-values text of `OptionArgumentReader.ProxyListenUrlText` names all six schemes, with 443 as the default port for `https` and 1080 for the other five.
- [x] The manual's PROXY OPTIONS section says that `-x`/`--proxy` also takes `socks4`, `socks4a`, `socks5` and `socks5h` listen URLs, with default port 1080, and every manual line stays within 79 columns.
- [x] A test in `Surl.Cli.UnitTests` fails when a scheme in `ProxyDefaultPorts` is missing from the allowed-values text of `ProxyListenUrlText`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The test is `OptionArgumentReaderTests.ProxyListenUrlText_AllowedValues_NameEveryProxySchemeAndItsDefaultPort`.
  It reads whole words from the clause before the first `;`, because `http`, `socks4` and `socks5` are substrings
  of other schemes and the default-port clause also names `https`. It failed on `socks4` before the fix.
- The text stays a literal rather than being built from `ProxyDefaultPorts`; the test guards the two agreeing.
- The manual section's later sentences were reworded so they stay true with SOCKS listed: the SOCKS proxy serves
  `CONNECT` only, shares the origin rule and the `-U` accounts, and `--proxy-auth` does not apply to it (the same
  facts as the `socks` paragraphs in `AiHelpProse`).
- Verified: build 0 warnings, `dotnet format --verify-no-changes` clean, fast tests all pass (`Surl.Cli.UnitTests`
  1983 of 1983). No integration-level code changed.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The --proxy argument type and the manual's PROXY OPTIONS section name all six proxy listen schemes and their default ports
