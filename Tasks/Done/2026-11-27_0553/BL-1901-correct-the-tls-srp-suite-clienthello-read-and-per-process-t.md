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
completed: 2026-11-27
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

- [x] The `ForTlsSrp` summary names both the RSA-signed and the DSS-signed suites and which key serves each.
- [x] The `ClientHelloPeek.ReadAsync` summary says when only the record header is read.
- [x] No comment or `CLAUDE.md` line in the Networking library, and none in `ServerTlsComposition.cs`, says there is
      one `ServerTlsSettings` per process.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- Checked against the code: `ServerTlsComposition.Compose` runs once per server start and builds at most one
  `ServerTlsSettings`; `ComposedServing` gives it to the listener factory and the data-connection opener.
- Also reworded in the two touched libraries, because each was the same claim or sat in a sentence being rewritten:
  `StreamConnection.cs` and `SurlServerCompositionSeams.cs` ("the process's"), `ServerTlsHandshake`'s summaries (the
  data-connection opener makes one too), `SocketListenerFactory`'s fields (it also holds a clock) and the
  `ServerTlsComposition` class summary (TLS-SRP). The rest of that file's comments are BL-1881's.
- `surl` refuses a non-RSA key under TLS-SRP (`TlsSrpCertificateOf`); the DSS-signed suites are reachable only by a
  caller of `ForTlsSrp`, which is what its summary describes.
- Verified 2026-11-27: build 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 30275 passed,
  3 skipped, 0 failed; no non-comment `.cs` line changed. No integration run (comments only).
- Filed BL-1909 for the same per-process claim in `Surl.Console` and the `QuicCertificate` summary.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. TLS-SRP suite, ClientHello read and per-start TLS settings comments match the code
