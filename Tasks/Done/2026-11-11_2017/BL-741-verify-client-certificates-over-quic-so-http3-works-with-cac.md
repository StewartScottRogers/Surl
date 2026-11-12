---
id: BL-741
title: Verify client certificates over QUIC so --http3 works with --cacert
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-384]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Protocols/Http/Surl.Http3.UnitTests, Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-741 — Verify client certificates over QUIC so --http3 works with --cacert

## Goal

`surl --http3 --cacert <file> https://...` serves HTTP/3 and requires a client certificate that
chains to the `--cacert` anchors, as the TCP listener does (ADR-0010 section 5).

## Context

- BL-384 refuses `--http3` with `--cacert` (`option --http3: cannot be used with --cacert`, exit
  2), because `QuicServerConnection` builds its `TlsServerHandshakeOptions` without
  `TrustsClientCertificateChain`, so an HTTP/3 connection would skip the client-certificate check
  the TCP side enforces.
- `Surl.Tls13`'s `TlsServerHandshakeOptions.TrustsClientCertificateChain` already exists;
  `QuicServerConnectionOptions` needs to pass it through, and `Http3ApplicationProtocol` should
  put the verified certificate on the `TlsSession` it starts the authentication session with
  (the `external` mechanism reads it).
- Upstream curl's `--cert` over `--http3`: measure with `Record-CurlExchange.ps1` before pinning.

## Acceptance criteria

- [x] `CommandLineParser` no longer refuses `--http3` with `--cacert`; its test and the
      `--aihelp http` paragraph and manual's HTTP OPTIONS say so.
- [x] A `Surl.Quic.UnitTests` test shows a handshake without a client certificate failing, and one
      with a chaining certificate succeeding, when the options carry the trust check.
- [x] `dotnet build -warnaserror` is clean, fast tests green, touched projects at 100% line and branch coverage.

## Notes

- Delivered in one session rather than through separate architect and implementer agents: the plan was small (pass an existing `Surl.Tls13` option through, put the certificate on the session, wire it in Composition, lift the refusal).
- Decision (ADR-0082 Amendment 5, decided by Claude under Stewart's delegation): `--http3 --cacert` is accepted. `QuicServerConnectionOptions.TrustsClientCertificateChain` feeds the TLS handshake's check; `Http3ProtocolServer` takes it as an optional constructor argument (`RequiresClientCertificate` says whether it has one); `Http3ApplicationProtocol` puts the verified end-entity certificate on `TlsSession.ClientCertificate`.
- Decision: the check lives in `Surl.Composition` as `QuicClientCertificateTrust`, judging the end-entity certificate the way `Surl.Networking`'s internal `ClientCertificateVerifier` judges a TCP one (anchors only, no revocation, no download, injected time, client-auth EKU when present). It is a copy rather than a reuse, because that class is internal to `Surl.Networking`, which is outside this task's `touches`. Intermediates the client sends are ignored, as on TCP.
- Touches widened: `Protocols/Http/Surl.Http3.UnitTests` (its `UnitTestTls13Client` and `UnitTestQuicClient` are linked into `Surl.Protocol.Http.UnitTests` and had to learn to answer a CertificateRequest) and ADR-0082 (the amendment). No task in Doing named either.
- Measured with pinned upstream curl 8.21.0 (`Record-CurlExchange.ps1 -NoServer` against a live `surl -v --http3 --cert --key --cacert`). `--http3-only --cacert --cert --key` exits 0 and gets the file. With no `--cert` it exits 55 (`ERR_DRAINING`). With a certificate lacking the client-auth EKU it also exits 55. Over TCP, no certificate gives 56.
- Coverage: Surl.Quic, Surl.Protocol.Http, Surl.Cli and Surl.Composition are at 100% line and 100% branch. `Surl.Console` shows a gap at `Program.cs:51`, which is outside this task and was already there.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl --http3 --cacert serves HTTP/3 and requires a client certificate chaining to the --cacert anchors, as TCP does
