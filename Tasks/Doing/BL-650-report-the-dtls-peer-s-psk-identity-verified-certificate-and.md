---
id: BL-650
title: Report the DTLS peer's PSK identity, verified certificate and record size from Surl.Dtls
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-415, BL-738]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-650 — Report the DTLS peer's PSK identity, verified certificate and record size from Surl.Dtls

## Goal

A completed `Surl.Dtls` server session tells its caller who the peer is - the PSK identity it used, or the
client certificate it presented and whether that certificate verified against the configured CA - and how many
application bytes one record can carry, so `coaps` (BL-475) can apply ADR-0097's access rules and block sizes.

## Context

- ADR-0097 (BL-470) decision 6: a `coaps` peer counts as authenticated only through a PSK identity from the
  `--user`/`--user-file` accounts or a client certificate checked against `--cacert`; the `-v` login note names
  method `psk`. Decision 4 and 7 size blocks and the largest message from the record payload the session allows.
- ADR-0087: the DTLS 1.2 server's modes (certificate, RawPublicKey, PSK), 1200-byte datagrams.
- BL-414 and BL-415 build the certificate and PSK handshakes but neither names this reporting; filed by BL-470.

## Acceptance criteria

- [ ] Named tests in `Surl.Dtls.UnitTests` show that after a PSK handshake the session reports the identity the
      client sent, as UTF-8 text.
- [ ] Named tests show that after a certificate handshake with a client certificate the session reports it and
      whether it verified against the configured trust anchors, and that without one it reports none.
- [ ] A named test shows the session reports the largest application payload one record carries under the
      negotiated suite and the 1200-byte datagram limit.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-05 (lane 2): `DtlsServerSession` sends no `CertificateRequest` and reads no client `Certificate`
  or `CertificateVerify` yet; BL-738 builds that handshake. The second acceptance criterion (report the client
  certificate and whether it verified) cannot be tested until then, so BL-738 is added to `depends-on`. The PSK
  identity and record-size reporting wait with it, to keep one session API change in one run.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Waits on BL-738: the client-certificate handshake it reports on does not exist yet
- 2026-11-05: Backlog -> Doing.
