---
id: BL-1809
title: Correct the SOCKS server CLAUDE.md's claims about the Unix domain socket and the GSS-API keytab
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1809 — Correct the SOCKS server CLAUDE.md's claims about the Unix domain socket and the GSS-API keytab

## Goal

`Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/CLAUDE.md` says what the code does about SOCKS5 on a Unix domain socket and about the GSS-API keytab.

## Context

Found by BL-1806. The file says the Unix domain socket (BL-561) "is still to come" and that no command-line option hands the server a keytab. BL-561 is in `Tasks/Done`, the `--aihelp` text describes both as working, and `ComposeSocksGssApiLogin` in `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs` wires the keytab.

## Acceptance criteria

- [x] The file states how `surl` serves SOCKS5 on a Unix domain socket, as `Surl.Composition` does it.
- [x] The file names the command-line option that hands the SOCKS server its keytab, as `ComposeSocksGssApiLogin` reads it.

## Notes

- Delivered by `align-and-document`: the two false claims are replaced by a Unix domain socket
  paragraph (`--unix-socket`, `--abstract-unix-socket`) and a GSS-API paragraph (`--keytab`, service
  `rcmd`, whatever `--auth` names). Checked against `ComposeSocksGssApiLogin`,
  `ProtocolServerRegistrations.Original` and `UnixSocketCombination`.
- Only Markdown changed. `dotnet build` had 0 warnings and 0 errors; the fast tests had 0 failures.
- Found outside this task and filed: BL-1817, BL-1818, BL-1819 (the `--keytab is unused` warning and
  help text ignore SOCKS5), BL-1820 (stale statements in two other `CLAUDE.md` files), BL-1816 (decide
  whether a SOCKS5 GSS-API login needs an account).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The SOCKS server CLAUDE.md now says how surl serves SOCKS5 on a Unix domain socket and that --keytab hands it its keys.
