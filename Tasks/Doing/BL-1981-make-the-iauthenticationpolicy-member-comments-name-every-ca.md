---
id: BL-1981
title: Make the IAuthenticationPolicy member comments name every caller truthfully
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary/IAuthenticationPolicy.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1981 — Make the IAuthenticationPolicy member comments name every caller truthfully

## Goal

The member summaries of `IAuthenticationPolicy` say who calls each member as the code does now.

## Context

Found while working BL-1962 (2026-11-27), read from source. XML doc comments only; no code or behaviour change. All in
`Foundation/Surl.Protocol.Abstractions.UnitLibrary/IAuthenticationPolicy.cs`; line numbers are as of that date.

- `CheckPasswordLoginAsync`, about line 15, says "MQTT's `CONNECT` now; FTP, IMAP, POP3, SMTP and LDAP later". All of
  those call it today (`FtpCommandResponder`, `ImapSession`, `Pop3Session`, `SmtpSession`, `LdapBindJudge`), as do the
  SOCKS proxy and the MQTT 5, Kafka, NATS, STOMP, Redis, Socket.IO, OCI registry and MCP servers.
- `StartHttpConnection(TlsSession?)`, about line 26, says "The HTTP server calls it once per connection". The HTTP/3,
  WebSocket, RTSP, HTTP proxy, S3, Google Cloud Storage, Azure Storage, SignalR, gRPC, WebTransport and MCP server code
  calls a `StartHttpConnection` overload too; check how often each calls it, and look for callers through shared
  libraries, before writing.
- `NamesAccount`, about line 48, gives the Azure Storage path-style check as its only use. `OciAccessCheck` and
  `McpAuthorizationGrants` also call it, to reject a token whose account no longer exists.

No store, event or fault is involved: comments only.

## Acceptance criteria

- [ ] The `CheckPasswordLoginAsync` summary says nothing is still to come and describes its callers as they are.
- [ ] The `StartHttpConnection(TlsSession?)` summary no longer says only the HTTP server calls it.
- [ ] The `NamesAccount` summary names the token check as well as the Azure Storage path-style check.
- [ ] The diff changes only `///` comment lines in `IAuthenticationPolicy.cs`.
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
