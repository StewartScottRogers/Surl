---
id: BL-961
title: Serve DNS over QUIC for --dns-transport quic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-959, BL-484, BL-384]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-961 — Serve DNS over QUIC for --dns-transport quic

## Goal

`surl --dns-transport quic --self-signed dns://127.0.0.1:0` serves DNS over QUIC (RFC 9250) with ALPN `doq`, as
ADR-0092 decision 5 says.

## Context

- Last of the four tasks BL-485 was split into (see BL-958). It adds `quic` to `--dns-transport`'s allowed
  values (BL-959 added the option) and composes the listener.
- Decision: ADR-0092 decision 5, the DoQ bullets: `DnsOverQuicProtocolServer` (BL-484, an
  `IQuicApplicationProtocol`) on `QuicDatagramFlowServer` with `QuicServerConnectionOptions` naming ALPN `doq` only
  (ADR-0086 decision 4); composed as BL-384 composes HTTP/3 (`QuicServerCertificate` in
  `Application/Surl.Composition.UnitLibrary`). Default port 853; TLS 1.3 only.
- Refusals before any listener binds: `udp` with `quic` on one explicit non-zero port, `FailedInit` (2),
  `surl: --dns-transport udp and quic cannot share port <n>`; `quic` on port 53,
  `surl: --dns-transport quic cannot use port 53` (RFC 9250 section 4.1.1). `quic` needs `--cert` or
  `--self-signed`, else `CertificateProblem` (58).
- The DoQ refusal past the connection limits is BL-832's, which waits on this task.
- Stand-in: no store; the DoQ exchanges are the engine's events.

## Acceptance criteria

- [ ] A named `Surl.Cli.UnitTests` test reads `--dns-transport udp,tcp,quic`; the option's allowed values in its
      `--aihelp` facts list `quic`.
- [ ] Named `Surl.Composition.UnitTests` tests show `quic` composing a QUIC listener with ALPN `doq` whose flows
      reach `DnsOverQuicProtocolServer`, `udp,quic` on one explicit port and `quic` on port 53 each refused with
      the decision's text.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass; `dotnet build -warnaserror` is clean, the
      fast tests are green, and the touched libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
