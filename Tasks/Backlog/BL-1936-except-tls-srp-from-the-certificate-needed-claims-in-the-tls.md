---
id: BL-1936
title: Except TLS-SRP from the certificate-needed claims in the TLS-first scheme documents
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1933, BL-1934]
touches: [README.md, Documentation/Product/Requirements.md, Documentation/Conformance/Extended/GoogleCloudStorage.md, Documentation/Conformance/Extended/McpServer.md, Documentation/Conformance/Extended/SignalR.md, Documentation/Conformance/Extended/SocketIo.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1936 — Except TLS-SRP from the certificate-needed claims in the TLS-first scheme documents

## Goal

The README, the requirements and the extended conformance documents do not say a TCP listen URL that TLS-SRP secures
needs a certificate.

## Context

Found while working BL-1924 (2026-11-27), read from source. Prose only; no behaviour change.

- Under TLS-SRP (`--tlsauthtype SRP` or `--tlsuser`) TCP listen URLs that are TLS from the first byte, and `natss`,
  are served with no certificate (ADR-0122 decision 5). QUIC and DTLS listeners still need one (ADR-0122
  Amendment 5): `webtransport`, DNS over QUIC and `coaps` with no account.
- Line numbers as of that date: `README.md` about lines 120, 325 and 375; `Documentation/Product/Requirements.md`
  about lines 47 and 58; and `GoogleCloudStorage.md`, `McpServer.md`, `SignalR.md` and `SocketIo.md` in
  `Documentation/Conformance/Extended`.
- BL-1933 and BL-1934 correct the TLS upgrade claims, some in the same files; do not redo them.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] No sentence in the six files in `touches` says a TCP listen URL needs `--cert` or `--self-signed`, or is refused `(58)` without one, without excepting TLS-SRP.
- [ ] Sentences about `webtransport`, DNS over QUIC and `coaps` with no account still say they need a certificate.
- [ ] Every test name a conformance table cites is unchanged and exists in the solution.

## Notes

## Log

- 2026-11-27: Created.
