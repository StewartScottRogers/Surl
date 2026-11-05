---
id: BL-527
title: Upgrade NATS connections to TLS for natss
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-526, BL-734]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-527 — Upgrade NATS connections to TLS for natss

## Goal

For `natss`, the NATS server sends INFO with `tls_required: true` and then upgrades the connection to TLS before
reading CONNECT, as the NATS protocol does. It also accepts a TLS-first handshake if BL-524's ADR decides
so, and refuses plain-text CONNECT where TLS is required.

## Context

- Decision: BL-524's ADR, decision 6.
- The TLS upgrade comes from the listener seam, as the mail servers' STARTTLS does (ADR-0010: how a protocol
  server upgrades a connection; `IsTlsUpgradeAvailable`, ADR-0069). The server never constructs an `SslStream`.
- The NATS page: INFO's `tls_required`, then the client starts TLS. `-ERR 'Secure Connection - TLS Required'`
  for a client that sends CONNECT in plain text.

## Acceptance criteria

- [x] Named tests, with the in-memory TLS upgrade the mail servers' tests use, show:
      - INFO with `tls_required` followed by an upgrade and then CONNECT accepted over TLS;
      - plain-text CONNECT after `tls_required` refused with the page's `-ERR`;
      - TLS-first accepted or refused as the ADR says;
      - `nats` (no TLS) never offering TLS when no certificate is configured.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built on ADR-0113 decision 6 and BL-734's `IConnection.PeekByteAsync`; no new ADR (every choice below sits inside
  decision 6). `NatsServer.Schemes` is now `nats` and `natss`; `NatsInfo.TlsRequired` is true on `natss`.
- On `natss`, `NatsServerConnection` settles TLS **before the outbox's writer starts**, writing INFO (INFO-first) and
  `-ERR 'Secure Connection - TLS Required'` straight to the connection, so no write is pending when
  `UpgradeToTlsAsync` runs (the socket connection refuses an upgrade with a pending write).
- Followed the ADR literally: **any** byte within the 50 ms TLS-first wait starts the handshake (plain text sent
  before INFO fails the handshake and closes with no bytes); only after INFO does a non-`0x16` byte get the `-ERR`.
- The head timeout is now one timer from accept, covering the handshake and ending with CONNECT's line (it is
  disposed there, so the keep-alive tests' timer counts are unchanged). No handshake begun by then is
  `-ERR 'Authorization Timeout'`; a handshake cut off part-way closes with no bytes; a failed one propagates
  `TlsHandshakeException`, which the serving engine notes, as SMTP's does. With the head timeout off, the after-INFO
  peek waits `NatsServerOptions.LongestTlsHandshakeWait` (the longest timer, about 49.7 days).
- "`nats` with no certificate" is covered by `nats` never peeking or upgrading at all (decision 6: no TLS with or
  without a certificate). Requiring `--cert`/`--self-signed` for `natss` (`CertificateProblem`) is composition's
  work, BL-528.
- Tests: `NatsServerConnectionTests.Tls.cs` (11) over `UnitTestTlsClientConnection`, a wrapper on
  `InMemoryConnection`'s stand-in upgrade whose first chunk (a stand-in ClientHello or plain text) the handshake
  consumes, plus `NatsInfoTests.Encode_TlsRequired_SaysTlsRequiredButNotAvailable`. `Measure-CodeQuality.ps1`:
  100% line, 100% branch, worst CRAP 10. Nats tests 181 passing; full fast run green.
- Follow-up filed: BL-826 traces Nats.md's `NATS-TLS-*` rows to these tests (`Documentation/Conformance` was held
  by BL-396, so not widened into this task).

## Log

- 2026-11-04: Created.
- 2026-11-05: Amended by BL-524: waits on BL-734 (peek before the TLS upgrade, ADR-0113 decision 6).
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. natss serves NATS over TLS through the listener seam, TLS first or INFO first, and refuses plain text after tls_required
