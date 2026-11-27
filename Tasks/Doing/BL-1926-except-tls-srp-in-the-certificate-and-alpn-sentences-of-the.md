---
id: BL-1926
title: Except TLS-SRP in the certificate and ALPN sentences of the remaining TLS topics in --aihelp
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1923]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1926 — Except TLS-SRP in the certificate and ALPN sentences of the remaining TLS topics in --aihelp

## Goal

No `--aihelp` paragraph says a TLS scheme on TCP needs `--cert` or `--self-signed` without excepting TLS-SRP, or
promises an ALPN protocol that a TLS-SRP handshake never agrees.

## Context

Found while working BL-1923 (2026-11-27), read from source. Help text and the tests that pin it; no behaviour change.
BL-1923 corrected the `amqp1`, `ftp`, `imap`, `ldap`, `pop3`, `smtp` and `xmpp` topics; follow its wording
(`--cert`, `--self-signed` or TLS-SRP).

- Under TLS-SRP, `ServerTlsComposition.IsSecuredByTlsSrp` lifts the certificate requirement from every TCP TLS
  listener. Only `webtransport`, DNS over QUIC and a `coaps` listener with no account still need one (ADR-0122
  Amendment 5).
- `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` still says "needs `--cert` or `--self-signed`" unqualified in the
  `amqp`, `azure-storage`, `dns` (the `tls` and `https` transports), `gopher`, `grpc`, `gs`, `http`, `kafka`, `mcp`,
  `mqtt`, `mqtt5`, `nats`, `oci`, `redis`, `s3`, `signalr`, `smb`, `socketio`, `stomp` and `websocket` paragraphs, and
  the first two `tls` paragraphs ("needs a certificate", "exits 58"). `grep` for `--self-signed` to confirm the list.
- `Cryptography/Surl.TlsSrp.UnitLibrary/TlsSrpNegotiation.ChooseApplicationProtocol` agrees only `http/1.1`: a client
  offering ALPN without it is refused `no_application_protocol`. So under TLS-SRP the `h2` that the `http`, `grpc`
  (`grpcs`) and `dns` paragraphs name, and `dot`, are never agreed. Say what happens there.

No store, event or fault is involved: help text only.

## Acceptance criteria

- [ ] No `AiHelpProse` sentence says a TLS-from-the-first-byte or upgrade-required TCP scheme needs a certificate without excepting TLS-SRP.
- [ ] The `webtransport`, DNS over QUIC and account-less `coaps` sentences still say a certificate is needed under TLS-SRP too.
- [ ] The `http`, `grpc` and `dns` paragraphs say which ALPN is agreed under TLS-SRP and what a client offering ALPN without `http/1.1` gets.
- [ ] Named tests in `Surl.Cli.UnitTests` pin the TLS-SRP wording of each changed topic.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
