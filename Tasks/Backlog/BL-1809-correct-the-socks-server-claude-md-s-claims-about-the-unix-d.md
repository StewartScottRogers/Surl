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
completed:
---
# BL-1809 — Correct the SOCKS server CLAUDE.md's claims about the Unix domain socket and the GSS-API keytab

## Goal

`Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/CLAUDE.md` says what the code does about SOCKS5 on a Unix domain socket and about the GSS-API keytab.

## Context

Found by BL-1806. The file says the Unix domain socket (BL-561) "is still to come" and that no command-line option hands the server a keytab. BL-561 is in `Tasks/Done`, the `--aihelp` text describes both as working, and `ComposeSocksGssApiLogin` in `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs` wires the keytab.

## Acceptance criteria

- [ ] The file states how `surl` serves SOCKS5 on a Unix domain socket, as `Surl.Composition` does it.
- [ ] The file names the command-line option that hands the SOCKS server its keytab, as `ComposeSocksGssApiLogin` reads it.

## Notes

## Log

- 2026-11-26: Created.
