---
id: BL-2156
title: Take --max-filesize 0 as no limit in a WebTransport session
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: NFR-016
created: 2026-11-28
completed:
---
# BL-2156 — Take --max-filesize 0 as no limit in a WebTransport session

## Goal

With `--max-filesize 0` a `webtransport` session receives any amount of payload, and with flow control on its `WT_MAX_DATA` keeps being raised (ADR-0006 section 1). This task changes nothing in `Documentation/Product/Requirements.md`; BL-2157 re-states NFR-014 to NFR-016.

## Context

Found while working BL-2120 (2026-11-28), in `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary`:

- `WebTransportSession.CountReceived` ends the session when the received total is `>= context.Limits.MaxUploadBytes`, so 0 closes it (`session data limit reached`) at the first payload.
- `WebTransportConnectHandler.FlowControlFor` passes `(ulong)context.Limits.MaxUploadBytes` as `maxData` to `WebTransportFlowControl`. `WebTransportReceiveLimits.TryConsume` never raises the data limit past it, so with 0 the limit stays at the initial window.

ADR-0117 decisions 4 and 8 hold the session to `--max-filesize`. Apply the same guard to any other use of the limit in the project. Stand-in: a test that turns the limit off can stream a large payload through a session; the faults, events and stores are unchanged.

## Acceptance criteria

- [ ] A test in `WebTransportSessionTests` with `MaxUploadBytes` 0 receives stream payload and datagrams without the session being closed with code 1.
- [ ] A test with flow control on and `MaxUploadBytes` 0 shows `WT_MAX_DATA` raised past the initial data window as payload is read.
- [ ] The existing tests for a non-zero `--max-filesize` still close the session and cap `WT_MAX_DATA` as before.
- [ ] `dotnet build Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
