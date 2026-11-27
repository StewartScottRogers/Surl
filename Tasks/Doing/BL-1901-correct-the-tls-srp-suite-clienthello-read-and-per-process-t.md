---
id: BL-1901
title: Correct the TLS-SRP suite, ClientHello read and per-process TLS settings comments
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1888]
touches: [Foundation/Surl.Networking.UnitLibrary, Application/Surl.Composition.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1901 — Correct the TLS-SRP suite, ClientHello read and per-process TLS settings comments

## Goal

The comments and `CLAUDE.md` lines about TLS-SRP certificate suites, the ClientHello read and how many
`ServerTlsSettings` a process has are true of the code.

## Context

Found during BL-1888 (checked against the code on 2026-11-27). Comments and prose only.

- `Foundation/Surl.Networking.UnitLibrary/ServerTlsSettings.cs`, `ForTlsSrp` XML summary: says a certificate adds
  the "RSA-signed" suites. `TlsSrpServerCertificate` (`Cryptography/Surl.TlsSrp.UnitLibrary`) takes an RSA or a DSA
  key, and a DSA key serves the DSS-signed suites.
- `Foundation/Surl.Networking.UnitLibrary/ClientHelloPeek.cs`, `ReadAsync` summary: says it reads the record's
  "header and fragment, or as much as the transport gives". For a record type other than handshake, or a length over
  16384, it reads the header only.
- "The process's TLS settings" or "one per process": `ServerTlsSettings.cs`, `ServerTlsHandshake.cs`,
  `SocketListenerFactory.cs`, `TcpConnectionListener.cs`, `UnixSocketConnectionListener.cs`,
  `SocketDataConnectionOpener.cs` and `CLAUDE.md` in the Networking library, and `ServerTlsComposition.cs` in
  `Application/Surl.Composition.UnitLibrary`. Settings are composed per server start, and `Surl.Api` runs many
  instances in one process. Check each place against the code before rewording it.

## Acceptance criteria

- [ ] The `ForTlsSrp` summary names both the RSA-signed and the DSS-signed suites and which key serves each.
- [ ] The `ClientHelloPeek.ReadAsync` summary says when only the record header is read.
- [ ] No comment or `CLAUDE.md` line in the Networking library, and none in `ServerTlsComposition.cs`, says there is
      one `ServerTlsSettings` per process.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
