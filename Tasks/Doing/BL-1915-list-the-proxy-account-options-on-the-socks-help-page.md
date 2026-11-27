---
id: BL-1915
title: List the proxy account options on the SOCKS help page
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1915 — List the proxy account options on the SOCKS help page

## Goal

`surl --help socks` and `surl --aihelp socks` list each proxy account option the SOCKS server reads.

## Context

- Found by BL-1902. `-U`/`--proxy-user` and `--proxy-user-file` are in the `auth` and `proxy` categories of
  `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs`, not `socks`, though the SOCKS paragraph in
  `AiHelpProse` says they name the accounts of every proxy listener, SOCKS included.
- Check `--keytab` (SOCKS5 GSS-API) and `--proxy-auth` against `Surl.Composition`'s `ComposeProxyPolicy` and
  `Surl.Protocol.Socks` before listing either.

## Acceptance criteria

- [ ] `surl --help socks` and `surl --aihelp socks` list `-U`, `--proxy-user-file`, and each of `--keytab` and `--proxy-auth` only if the code shows the SOCKS server reads it.
- [ ] A named test in `Surl.Cli.UnitTests` pins the list.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
