---
id: BL-1060
title: Wait for the client to close each WebTransport CONNECT stream before CONNECTION_CLOSE at shutdown
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-895]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-1060 — Wait for the client to close each WebTransport CONNECT stream before CONNECTION_CLOSE at shutdown

## Goal

At shutdown the WebTransport server waits, within the grace, for the client to close each CONNECT stream before it closes the connection (WTH3 6, matrix row WTH3-6-5).

## Context

- BL-895 sends `WT_DRAIN_SESSION`, `GOAWAY`, serves through the grace, then `WT_CLOSE_SESSION` code 0 and FIN; the connection closes once the server side of the CONNECT streams is done and does not wait for the client's own FIN.
- Row WTH3-6-5 of `Documentation/Conformance/Extended/WebTransport.md`.

## Acceptance criteria

- [ ] A named test shows the connection closing only after the client FINs its CONNECT stream, or when the grace has passed, whichever is first.
- [ ] Row WTH3-6-5 names that test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-11: Created.
