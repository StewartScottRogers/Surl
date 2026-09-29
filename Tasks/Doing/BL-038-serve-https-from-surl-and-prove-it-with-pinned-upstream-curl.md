---
id: BL-038
title: Serve https from surl and prove it with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-012, BL-019, BL-020, BL-062, BL-064, BL-065, BL-067]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-038 — Serve https from surl and prove it with pinned upstream curl

## Goal

`surl --cert … --key … https://127.0.0.1:<port>/` serves the served directory over
HTTP/1.1 on TLS. With no certificate given, it does what the TLS ADR (BL-002) decides.
The pinned upstream curl 8.21.0 build fetches from it, as integration tests in
`Surl.Conformance.UnitTests` prove.

## Context

- The TLS ADR recorded by BL-002 (contract added by BL-006, implemented by BL-012) says
  where the certificate comes from, what happens without one, and which ALPN IDs are
  offered. The command-line ADR (BL-003) gives `--cert`, `--key` and `--cacert`, parsed
  by BL-014. Both are indexed in `Documentation/Planning/Decisions/README.md`.
- ADR-0002: `https` is the same HTTP server (BL-018) over a secured connection. This
  task registers it for `https` in `Surl.Console`'s composition and has `Surl.Networking`
  secure the connection. It adds no second HTTP server.
- BL-020 built the process runner, the in-process `surl` start, and the
  `Assert.Inconclusive` rule for a platform without a pinned build. Reuse them.
- The pinned build is Schannel (`UpstreamCurlBuilds.json`). Schannel's certificate
  revocation checks can fail against a self-signed test certificate. Measure what the
  pinned build does with `--cacert <test CA>` and with `-k`, and use what the measurement
  supports. Record the finding in the test's comment.
- Certificates for tests are generated with `CertificateRequest` at test time and
  written to a temporary directory for curl's `--cacert`. Nothing is committed.

## Acceptance criteria

- [ ] `Surl.Console` registers the HTTP server for `https` and applies the TLS options
      as the TLS ADR says. Fast tests in `Surl.Console.UnitTests` prove the composition
      with fakes, including the no-certificate case.
- [ ] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build against `https://127.0.0.1:<P>/hello.txt`: it exits 0 with the file's bytes
      when trusting the test CA (or with `-k`, per the measurement), and exits with the
      `CURLE_*` code the measurement shows when not trusting it. The code is named in
      the test.
- [ ] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl` or
      `Surl.Conformance.UnitLibrary`.

## Notes

- 2026-09-28 (lane 3): Not startable yet. `https` needs four unfinished tasks: BL-065 (the
  serving engine performs the implicit handshake; today it never calls
  `UpgradeToTlsAsync`), BL-067 (load `--cert`, `--key` and `--cacert` into
  `ServerTlsSettings`), BL-064 (`CertificateProblem` 58 and `CaCertificateBadFile` 77 for
  the bad-file cases) and BL-062 (`surl` composes `SocketListenerFactory`, which takes the
  TLS settings, in place of `TcpListenerFactory`). Added to `depends-on`; no code changed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Backlog. Waits on BL-062, BL-064, BL-065 and BL-067: engine handshake, cert loading, exit codes 58/77 and the TLS-capable listener factory
- 2026-09-29: Backlog -> Doing.
