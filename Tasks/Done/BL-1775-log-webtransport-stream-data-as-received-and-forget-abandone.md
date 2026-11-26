---
id: BL-1775
title: Log WebTransport stream data as received and forget abandoned HTTP/3 streams in the exchange log redactor
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1762]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-26
---
# BL-1775 — Log WebTransport stream data as received and forget abandoned HTTP/3 streams in the exchange log redactor

## Goal

The WebTransport server's exchange log shows stream data as received, and streams reset or finished part-way through a frame do not make the redactor withhold later requests.

## Context

- Accepted limits of ADR-0182 amendment 21 (BL-1762), found by its code review.
- `Http3RequestBytesRedactor.RedactStream` reads every client bidirectional stream as HTTP/3 frames. A WebTransport
  stream (signal `0x41`, session ID, then data) is therefore misread, and data can be left out of the log.
- A stream that ends part-way through a frame stays remembered; after 4096 of them every later bidirectional chunk of
  the connection is logged as `****`.
- The plain HTTP/3 server must keep reading `0x41` as an unknown frame type, because a `HEADERS` frame may follow it.

## Acceptance criteria

- [x] A fast test sends a WebTransport bidirectional stream in several chunks and shows its data in the notes as received.
- [x] A fast test opens more than 4096 such streams on one connection and shows a later request's non-secret frames logged as received.
- [x] A fast test shows the plain HTTP/3 redactor still withholds a secret `HEADERS` frame that follows a `0x41` frame.
- [x] An ADR-0182 amendment states the rule, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

- Rule and rejected options: ADR-0182 amendment 35, decided by Claude under Stewart's delegation.
- The redactor takes the session markers from whoever makes it (WebTransport: `0x41`, `0x54`); the HTTP server gives
  none. Unidirectional session streams are covered too, because the goal says "stream data".
- Abandoned streams are forgotten by bound, not by an end-of-stream signal: at most 4,096 streams per direction, the
  lowest ID forgotten first, and only streams at or below it are then withheld (bidirectional) or judged alone.
  A signal would have needed new contracts in `Surl.Quic`, `Surl.Core` and `Surl.Protocol.Abstractions`.
- Tests: acceptance 1 `ServeAsync_SessionStreamSentInSeveralChunks_IsLoggedByTheServersRedactorAsReceived` (real server,
  a test sink that redacts as the engine does); 2 `CreateRedactor_MoreSessionStreamsThanItRemembers_StillLogsALaterRequestsFramesAsReceived`;
  3 `RedactAndRedactStream_APlainRedactor_WithholdsASecretHeadersFrameThatFollowsAnUnknown0x41Frame`.
- Code review: no must-fix; four wording fixes applied. Conformance stage skipped: no option, exit code or wire byte
  changes. No `--aihelp` or manual text describes the changed behaviour, so `Surl.Cli` is unchanged.
- Follow-up: BL-1787 (datagrams and `DATA` payloads are still read as frames).

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. WebTransport session stream data is logged as received, and abandoned HTTP/3 streams are forgotten instead of withholding the whole connection (ADR-0182 amendment 35)
