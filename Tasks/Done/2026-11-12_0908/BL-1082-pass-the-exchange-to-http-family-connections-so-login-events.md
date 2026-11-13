---
id: BL-1082
title: Pass the exchange to HTTP-family connections so login events name the real scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-871]
touches: [Protocols/Http]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1082 — Pass the exchange to HTTP-family connections so login events name the real scheme

## Goal

Every HTTP-family server starts its connection's session with `StartHttpConnection(tlsSession, LoginExchange.Of(context))`, so an `https` login is published as `https`, not `http`.

## Context

BL-871 (ADR-0128 Amendment 1) added `IAuthenticationPolicy.StartHttpConnection(TlsSession?, LoginExchange?)`. Until the servers call it, logins are published as `http` with no exchange ID. Servers that share `Surl.HttpServing` change there once.

## Acceptance criteria

- [x] A named test shows the HTTP serving layer starting the session with the connection's exchange.
- [x] The whole solution builds and the fast tests are green.

## Notes

Changed every server under Protocols/Http that starts an HTTP session: Http (HTTP/1, 2, 3), HttpProxy, Ws, Rtsp, SignalR, McpServer, WebTransport. Test: AuthenticationTests.ServeAsync_Connection_StartsTheAuthenticationSessionWithTheConnectionsExchange (test policy records the exchange). S3, GoogleCloudStorage and AzureStorage live outside touches (Protocols/FileTransfer) and still call the one-argument overload; they need a follow-up task.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. HTTP-family servers under Protocols/Http start sessions with the connection's exchange
