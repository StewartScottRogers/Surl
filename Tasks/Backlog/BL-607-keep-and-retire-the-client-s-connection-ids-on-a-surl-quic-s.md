---
id: BL-607
title: Keep and retire the client's connection IDs on a Surl.Quic server connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-377]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-607 — Keep and retire the client's connection IDs on a Surl.Quic server connection

## Goal

A `QuicServerConnection` keeps the client's `NEW_CONNECTION_ID`s up to the default
`active_connection_id_limit` of 2, retires those `retire_prior_to` names with
`RETIRE_CONNECTION_ID`, and moves to a connection ID still active when the one in use is retired.

## Context

- ADR-0082 decision 4, "Connection IDs"; RFC 9000 sections 5.1.1, 5.1.2, 19.15 and 19.16.
- BL-377 takes `NEW_CONNECTION_ID` and does nothing with it (the server keeps using the client's
  first Source Connection ID), and treats every `RETIRE_CONNECTION_ID` as `PROTOCOL_VIOLATION`, which
  is right while the server issues only its handshake connection ID.
- Start at `ProcessOtherFrame` in `Protocols/Http/Surl.Quic.UnitLibrary/QuicServerConnection.Receiving.cs`.

## Acceptance criteria

- [ ] Named tests: a third active connection ID is `CONNECTION_ID_LIMIT_ERROR`; a `retire_prior_to`
      covering the ID in use gets `RETIRE_CONNECTION_ID` and the server's next 1-RTT packet carries the
      remaining ID; a repeated `NEW_CONNECTION_ID` is ignored; one with a known sequence number but a
      different ID is `PROTOCOL_VIOLATION`; `NEW_CONNECTION_ID` from a client using a zero-length
      connection ID is `PROTOCOL_VIOLATION`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
