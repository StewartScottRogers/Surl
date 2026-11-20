---
id: BL-1465
title: Withhold credentials carried by the HTTP-based extended servers from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1421]
touches: [Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1465 — Withhold credentials carried by the HTTP-based extended servers from the exchange log

## Goal

With the BL-1418 contract implemented, ADR-0182's replacement text is logged in place of the `Authorization`, `Proxy-Authorization` and API-key headers (S3 and the other cloud-storage servers, MCP, SignalR, Socket.IO, gRPC, WebTransport, DNS over HTTPS) on the shared HTTP serving layer, reusing the BL-1421 head redactor on -v, --trace, --trace-ascii and --log-file.

## Context

- Filed by BL-1417 (ADR-0182): an extended server whose client bytes carry a secret in recoverable form. The server's own parser shows where the field sits; the redactor must work across chunk boundaries and keep the user name visible.
- Extended servers are judged by their protocol's specification (ADR-0083), not the Curl port.

## Acceptance criteria

- [x] A fast test in the server's .UnitTests shows the redacted bytes for a split-chunk exchange and that a server answer is unchanged.
- [x] The server's raw bytes still reach the server unchanged and the event stream follows ADR-0182.

## Notes

- Added `HttpExchangeRedaction` (HttpServing): HttpRequestHeadBytesRedactor plus API-key fields (X-Api-Key, Api-Key, X-Goog-Api-Key, X-Amz-Security-Token, Ocp-Apim-Subscription-Key). Wired S3, AzureStorage, GoogleCloudStorage, OciRegistry, McpServer, SignalR, SocketIo (touches extended; no Doing task overlapped).
- gRPC (HTTP/2), WebTransport (HTTP/3) and DoH need other redactors: filed BL-1467.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. HTTP/1.1 extended servers (S3, Azure, GCS, OCI, MCP, SignalR, Socket.IO) log credentials as ****; gRPC/WebTransport/DoH split to BL-1467
