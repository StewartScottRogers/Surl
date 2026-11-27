---
id: BL-1849
title: Correct the remaining TLS 1.3 routing and upstream test-case harness statements in Networking and the READMEs
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary, README.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1849 — Correct the remaining TLS 1.3 routing and upstream test-case harness statements in Networking and the READMEs

## Goal

The Networking `CLAUDE.md`, the `ServerTlsHandshake.AuthenticateAsync` XML comment, the README and the product overview describe the TLS 1.3 routing and the upstream test-case harness as the code is.

## Context

Found by BL-1837. Check each against the code before editing:

- `Foundation/Surl.Networking.UnitLibrary/CLAUDE.md`, bullet "TLS 1.3 where `SslStream` cannot serve it", says anything but a TLS-1.3-only ClientHello goes to `SslStream`. `ServerTlsHandshake.cs` also serves, on a listener limited to TLS 1.3, every ClientHello that offers TLS 1.3, and refuses a parsed one without it with a `protocol_version` alert (ADR-0157 amendment 1).
- The XML comment on `ServerTlsHandshake.AuthenticateAsync` says TLS-SRP "otherwise `SslStream`" and that the stream returned is an `SslStream` or a `TlsSrpStream`; both leave out `Tls13ServerStream`.
- `README.md` ("What is built") and `Documentation/Product/Product-Overview.md` ("Built for Phase 6") say `Surl.Conformance` plays the server half of upstream curl's test cases. `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` says that library does not hold the server halves: the protocol libraries carry them and the test project composes them.

## Acceptance criteria

- [x] The Networking `CLAUDE.md` TLS 1.3 bullet states what a listener limited to TLS 1.3 does with a ClientHello that offers TLS 1.3 and with one that does not.
- [x] The XML comment on `ServerTlsHandshake.AuthenticateAsync` names all three server paths and every stream type it returns.
- [x] `README.md` and the product overview say which projects carry the server halves of upstream curl's test cases and which composes them.

## Notes

- Documentation only: `.md` files and one XML comment; no behaviour change.
- The server halves are in three places: the protocol libraries' upstream test-case seams,
  Surl's ordinary servers (DICT, SCP, SFTP, SMB), and the test project (`httptls+srp`).
  `Surl.Conformance.UnitTests` composes them; `Surl.Conformance.UnitLibrary` holds none.
- Verified 2026-11-27: build with 0 warnings, format check clean, fast tests 30,202 passed,
  0 failed, 3 skipped.
- Filed, outside this task: BL-1859 (no `close_notify` before FIN on the hand-built TLS 1.3
  path, found by reading), BL-1860, BL-1861, BL-1862. The stale TLS-SRP listener remark in
  `UpstreamTestCaseTlsSrpServerHalf` is already BL-1855.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Networking docs and XML comment, README and product overview now describe the TLS 1.3 routing and upstream test-case server halves as the code is
