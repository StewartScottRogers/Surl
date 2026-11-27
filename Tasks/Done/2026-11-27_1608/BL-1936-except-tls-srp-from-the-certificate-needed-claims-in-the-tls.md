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
completed: 2026-11-27
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

- [x] No sentence in the six files in `touches` says a TCP listen URL needs `--cert` or `--self-signed`, or is refused `(58)` without one, without excepting TLS-SRP.
- [x] Sentences about `webtransport`, DNS over QUIC and `coaps` with no account still say they need a certificate.
- [x] Every test name a conformance table cites is unchanged and exists in the solution.

## Notes

- Corrected: `README.md` (secure listen URL, `wss`, `smbs`), FR-010, FR-021, and the `gs`, `mcps`, `signalrs` and
  `socketios` (two rows) conformance rows. Checked against ADR-0122 decision 5 and Amendment 5 and
  `ServerTlsComposition.FindListenUrlWithoutCertificate`; `gs`, `mcps`, `signalrs`, `socketios`, `wss` and `smbs` are
  all in `TlsSchemes`.
- The new exceptions name TCP listen URLs or TLS-first TCP schemes only. No sentence about `webtransport`, DNS over
  QUIC or `coaps` needing a certificate was edited.
- `McpServer.md` cited `RunAsync_McpsWithSelfSigned_ListensAtItsDefaultPort`, which does not exist; the method is
  `RunAsync_McpsWithSelfSigned_ListensAtItsDefaultPort443`. Corrected the citation so the third criterion's "exists"
  holds. No other cited test name changed; the cited test classes and methods in the four documents were checked
  against the test sources.
- Edited directly instead of delegating to `align-and-document`: six sentences-sized edits.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: 0 failed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The README, FR-010, FR-021 and the gs, mcps, signalrs and socketios conformance rows except TLS-SRP from the certificate-needed claims
