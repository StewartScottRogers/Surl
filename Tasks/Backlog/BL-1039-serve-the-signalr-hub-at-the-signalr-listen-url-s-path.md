---
id: BL-1039
title: Serve the SignalR hub at the signalr listen URL's path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-450]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-11
completed:
---
# BL-1039 — Serve the SignalR hub at the signalr listen URL's path

## Goal

`surl signalr://127.0.0.1:0/chat` serves the hub at `/chat` (negotiate at `/chat/negotiate`), and two
`signalr` listen URLs in one run each serve their own hub, as ADR-0119 decisions 0 and 1 say.

## Context

- BL-450 registers the SignalR server in `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs`
  with `SignalRHubPath.FromListenPath(string.Empty)`: one server, one `SignalRHubConnections`, hub at `/`
  for every `signalr`/`signalrs` listen URL.
- The obstacle: `ListenUrlParser` refuses a path (`A listen URL cannot have a path, query or fragment`,
  ADR-0007 section 4) and `ListenUrl` (`Surl.Protocol.Abstractions`) carries none, and a
  `ProtocolServerRegistration` builds one server per scheme, not per listen URL.
- Needs an ADR (or an ADR-0119 amendment) deciding how a listen URL may carry a path for the schemes
  that use one (signalr; perhaps socketio, mcp, grpc later) without loosening ADR-0007 for the others,
  and how the composition builds one hub per listen URL.

## Acceptance criteria

- [ ] An ADR records which schemes accept a listen URL path and how it reaches the server.
- [ ] A named `ListenUrlParserTests` test parses `signalr://127.0.0.1:0/chat/` and keeps the path; `http://127.0.0.1/x` is still refused exit 3.
- [ ] A named `Surl.Console.UnitTests` test starts `signalr://127.0.0.1:0/chat` and its server's `SignalRHubPath.EndpointBase` is `/chat`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-11: Created.
