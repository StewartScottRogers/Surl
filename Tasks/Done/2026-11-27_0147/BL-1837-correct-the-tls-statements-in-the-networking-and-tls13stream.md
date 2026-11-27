---
id: BL-1837
title: Correct the TLS statements in the Networking and Tls13Stream CLAUDE.md files and the product overview, and the README Conformance row
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary/CLAUDE.md, Cryptography/Surl.Tls13Stream.UnitLibrary/CLAUDE.md, Documentation/Product/Product-Overview.md, README.md]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1837 — Correct the TLS statements in the Networking and Tls13Stream CLAUDE.md files and the product overview, and the README Conformance row

## Goal

The Networking and Tls13Stream `CLAUDE.md` files, the product overview's "Architecture" section and the README `Conformance/` row describe the code as it is.

## Context

Found by BL-1811. Check each against the code before editing:

- `Cryptography/Surl.Tls13Stream.UnitLibrary/CLAUDE.md` says `Surl.Networking` does not yet set the client-certificate delegate (BL-1470); `Foundation/Surl.Networking.UnitLibrary/CLAUDE.md` says BL-1470 does it.
- The opening paragraph of the Networking `CLAUDE.md` places the HTTP and SOCKS proxy servers in that library; they are separate protocol libraries.
- The "Architecture" section of `Documentation/Product/Product-Overview.md` says server-side TLS uses `SslStream`, leaving out the `Surl.TlsSrp` and `Tls13ServerStream` paths.
- The README "Repository layout" `Conformance/` row mentions only the tests against a live `surl`, not the upstream test-case and Curl port harness code.

## Acceptance criteria

- [x] The two `CLAUDE.md` files agree on whether `Surl.Networking` sets the client-certificate delegate, and agree with the code.
- [x] The opening paragraph of the Networking `CLAUDE.md` places no proxy protocol server in that library.
- [x] The product overview's "Architecture" section names the TLS-SRP and hand-built TLS 1.3 server paths beside `SslStream`.
- [x] The README `Conformance/` row names what the projects in that folder hold.

## Notes

- The code sets the delegate: `ServerTlsSettings.CreateTls13Options` sets
  `TrustsClientCertificateChain` to `ClientCertificateVerifier.IsChainTrusted` when there are
  trust anchors. The Tls13Stream `CLAUDE.md` was the stale one; the Networking text on BL-1470
  was already right.
- Delivered by `align-and-document`; diff checked against `ServerTlsHandshake.cs`. No `.cs` or
  project file changed. `dotnet build` clean (0 warnings), fast tests green in every test project
  (0 failed).
- Other stale statements outside this task's criteria are filed as BL-1849.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Networking and Tls13Stream CLAUDE.md files, the product overview Architecture section and the README Conformance row describe the code as it is
