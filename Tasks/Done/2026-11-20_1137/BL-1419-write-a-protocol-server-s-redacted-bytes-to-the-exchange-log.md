---
id: BL-1419
title: Write a protocol server's redacted bytes to the exchange log in Surl.Core's serving engine
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1418]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1419 — Write a protocol server's redacted bytes to the exchange log in Surl.Core's serving engine

## Goal

When a protocol server implements BL-1418's contract, the serving engine writes the redacted form of
what it marks secret to the exchange log (`-v`, `--trace`, `--trace-ascii`, `--log-file`), and the
event stream gets what BL-1417's ADR says.

## Context

- `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:875-895` (`OpenExchange`) creates the log and
  wraps it in `EventPublishingExchangeLog(log, events, exchangeId)`. Bytes are recorded at
  `RecordingConnection.cs:32`, `RecordingDatagramFlow.cs:33`, `ServingEngine.Datagrams.cs:83` and, for
  FTP data connections, `ExchangeDataConnections.cs:80`. QUIC and DTLS plaintext is written as a hex
  note by `EventPublishingDecryptedBytesSink.cs`.
- `ExchangeEndedEvent`'s byte counts (`EventPublishingExchangeLog.ReceivedByteCount`) stay the wire
  counts.
- The engine finds the server for a listen URL's scheme; that is where the optional interface is asked
  for a per-exchange redactor (BL-1417's ADR fixes the exact place).

## Acceptance criteria

- [x] A fast test in `Foundation/Surl.Core.UnitTests`, over `InMemoryConnection` with a fake server
      whose redactor marks `secret`, shows the level's log receives the redacted bytes and
      `ExchangeEndedEvent` counts the wire bytes.
- [x] `ExchangeBytesEvent` carries raw or redacted bytes exactly as the ADR says (named test).
- [x] A server that does not implement the contract is logged byte for byte as before: the existing
      Core tests pass unchanged.
- [x] The decrypted-bytes note (`EventPublishingDecryptedBytesSink`) and FTP data-connection bytes are
      redacted or left as the ADR says (one named test each).
- [x] `dotnet build Foundation/Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are green,
      and coverage stays at 100% line and branch.

## Notes

- Added `RedactingExchangeLog` (below `EventPublishingExchangeLog`, one lock), `EventPublishingExchangeLog.PayloadLog`
  (data connections, raw) and a redacting path in `EventPublishingDecryptedBytesSink`. Decisions recorded as ADR-0182
  amendment 1. Tests: `ServingEngineTests.Redaction.cs`, `FakeRedactingConnectionServer.cs`. Coverage measured by reasoning
  over the branches, not by the collector.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Serving engine writes a redacting server's redacted received bytes to the level log; events and wire counts stay raw
