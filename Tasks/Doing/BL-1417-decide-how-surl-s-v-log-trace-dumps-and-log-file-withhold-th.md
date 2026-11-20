---
id: BL-1417
title: Decide how surl's -v log, trace dumps and --log-file withhold the passwords and tokens a client sends
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed:
---
# BL-1417 — Decide how surl's -v log, trace dumps and --log-file withhold the passwords and tokens a client sends

## Goal

An ADR, "Decided by Claude under Stewart's delegation", fixes what surl's `-v` log, `--trace` and
`--trace-ascii` dumps and `--log-file` write in place of the passwords and tokens a client sends, on
which sinks, and through which seam, so BL-1418 to BL-1435 can be built against it.

## Context

- Surly cookbook bug report `2026-11-20T0553-Surl-bug-report.md`, item 1, High (Surl `763754785`;
  the client was the Curl port, which is never an oracle): `surl -v --user alice:s3cret ftps://...`
  against `curl -k -u alice:s3cret ftps://...` logs `PASS s3cret`; over HTTP `-v` shows the
  `Authorization: Basic` and `Bearer` values. A log kept from a failed CI run leaks them. Production
  code is unchanged since `763754785` (24 commits, conformance tests and tasks only).
- Where it happens: `Foundation/Surl.Core.UnitLibrary/RecordingConnection.cs:32` hands every received
  chunk, raw, to `EventPublishingExchangeLog` (`EventPublishingExchangeLog.cs:34`), which forwards it to
  the level's `IExchangeLog`: `Foundation/Surl.Output.UnitLibrary/VerboseExchangeLog.cs` (`-v`) or
  `TraceExchangeLog.cs` (`--trace`, `--trace-ascii`). Both sit above TLS (ADR-0033 section 4), so FTPS
  and HTTPS leak too. QUIC and DTLS plaintext reaches the log as a hex note from
  `EventPublishingDecryptedBytesSink.cs` (ADR-0169), which is how HTTP/3 header blocks are logged.
- Bytes arrive in arbitrary chunks before any server parses them, and
  `IExchangeLogFactory.Create(exchangeId, remoteEndPoint)` (`ServingEngine.cs:879`) does not know the
  scheme. A redactor must buffer to a line or frame boundary, and sometimes needs what the server sent
  (after an SMTP `334` or IMAP `+` the next client line is a secret).
- Upstream curl 8.21.0 (the pinned win-x64 reference build, SHA-256 `0E7737...8778`, its own
  `--manual`) does not redact its client-side log. Under `--trace-ascii` it warns: "Note that verbose
  output of curl activities and network traffic might contain sensitive data, including usernames,
  credentials or secret data content." That is a client printing its own secret. A server's log
  holds every client's secrets and is kept, so secure by default (root `CLAUDE.md`, ADR-0032) wins.
- The original servers that carry a secret in clear, each with a filed task: FTP `PASS`/`ACCT`
  (BL-1420); HTTP/1.x `Authorization` and `Proxy-Authorization` (BL-1421 shared redactor; BL-1422
  http/https, BL-1425 proxy, BL-1426 rtsp, BL-1427 ws); HTTP/2 HPACK (BL-1423); HTTP/3 QPACK
  (BL-1424); SASL continuation lines (BL-1428) for SMTP `AUTH` (BL-1429), IMAP `LOGIN`/`AUTHENTICATE`
  (BL-1430) and POP3 `PASS`/`AUTH` (BL-1431); LDAP simple bind (BL-1432); MQTT `CONNECT` (BL-1433);
  SOCKS5 RFC 1929 (BL-1434); and, extended, Redis `AUTH` (BL-1435).
- Extended servers with no task yet: NATS `CONNECT` `pass`/`auth_token`
  (`Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsConnectOptions.cs:34`), STOMP
  `passcode` (`StompServerConnection.cs:146`), AMQP 0-9-1 PLAIN/AMQPLAIN
  (`Amqp0ConnectionStartOk.cs:59`), AMQP 1.0, Kafka and XMPP SASL PLAIN, MQTT 5 `CONNECT`, and the
  HTTP-carried servers on `Surl.HttpServing`/`Surl.HttpMessage` (S3, Azure Storage, Cloud Storage, OCI,
  MCP, SignalR, Socket.IO, gRPC, WebTransport, DNS over HTTPS).
- Probably not secrets in clear (the ADR confirms each): SSH (logged below its own encryption), SMB
  NTLM, HTTP Digest, NTLM and Negotiate, POP3 APOP, CRAM-MD5, SCRAM, TLS-SRP, S3 SigV4 signatures.
- The filed tasks assume this shape; if the ADR picks another, amend their `touches` before they start:
  an optional interface in `Surl.Protocol.Abstractions` that a protocol server implements to hand out a
  per-exchange redactor (BL-1418), applied by `Surl.Core`'s engine between the recording decorators
  and the levelled or trace log (BL-1419). Seams are interfaces, not a central switch (root `CLAUDE.md`,
  "Open for extension").

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions` (the next free number), marked "Decided by Claude
      under Stewart's delegation", fixes what replaces a secret (for example `PASS ****`,
      `Authorization: Basic ****`), whether its length shows, and what a trace dump's `<n> bytes`
      header counts after redaction.
- [ ] It names the sinks that redact (`-v`, `--trace`, `--trace-ascii`, `--log-file`, `Surl.Api`'s
      `Log`). It decides, with reasons, whether `ExchangeBytesEvent` on the event stream (`Surl.Api`
      `Exchanges`, surl-mcp `get_exchanges`, control endpoint events) carries raw or redacted bytes, and
      whether any option turns redaction off. If one does, it is a loosening that warns (ADR-0032
      section 9).
- [ ] It fixes the seam: interface names; where the engine applies it; how a redactor sees sent bytes
      for SASL state; binary frames; chunk boundaries and the most bytes a redactor may hold back. It
      states that `RecordingExchangeLog` and the upstream-test-case server halves in `Surl.Conformance`
      keep raw bytes.
- [ ] It has a table: every server whose client bytes carry a password, token or key in recoverable
      form, the field, and the BL task that redacts it. For each server in the table with no task yet
      (the extended list above), it files one with `task-board.ps1 new`, depending on BL-1419, and
      names it in the table.
- [ ] It quotes what upstream curl 8.21.0's `--manual` says about its own `-v`/`--trace-ascii` and says
      why surl differs.
- [ ] `Documentation/Planning/Decisions/README.md` lists the ADR.

## Notes

- Leaning: redact every console and log-file sink. Keep raw bytes on the in-process event stream for
  `Surl.Api`, where the test owns the secret, and decide surl-mcp's `get_exchanges` and the control
  endpoint separately, since their output can end up in an agent's transcript or a CI artefact.
- A user name stays visible unless the ADR finds a reason otherwise.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
