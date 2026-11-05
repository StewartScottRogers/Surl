---
id: BL-448
title: Create the SignalR server and answer negotiate with the JSON hub message codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-447, BL-404, BL-407]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Surl.slnx, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-448 — Create the SignalR server and answer negotiate with the JSON hub message codec

## Goal

`Surl.Extended.Protocol.SignalR.UnitLibrary` exists. It answers `POST <hub>/negotiate?negotiateVersion=1` with
the negotiate JSON, checks the bearer login, and reads and writes every JSON hub-protocol message with its
record separator.

## Context

- Decision: BL-447's ADR, decisions 1, 2, 3 and 5. Rules: BL-543's ADR.
- New projects in `Protocols/Http/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.SignalR`. References:
  `Surl.Protocol.Abstractions` and BL-407's library. BL-449 adds the WebSocket library.
- TransportProtocols.md "`POST [endpoint-base]/negotiate`" (the response's fields and its `error` form), and
  HubProtocol.md "JSON Encoding" (each message type's fields, the `0x1E` terminator, several messages in one
  frame, and a message split across frames).
- JSON through `Utf8JsonReader`/`Utf8JsonWriter` or a source-generated context (native AOT).

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - negotiate answered with a fresh `connectionId` and `connectionToken` and the ADR's transports;
      - negotiate without a token refused as the ADR says;
      - each message type round-tripped against the JSON examples in HubProtocol.md;
      - two messages in one buffer read separately;
      - a message split across two buffers reassembled;
      - a message over the limit refused.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Delivered (ADR-0119 decisions 0, 1, 2, 3, 5 and the record limits of 7): `SignalRProtocolServer`
  (schemes `signalr`, `signalrs`) over `Http1ConnectionServer`; `SignalRRequestAnswerer` (login first,
  negotiate versions 0 and 1, `400`/`405`/`404`, the hub-full `error` form); `SignalRHubConnections`
  (16-byte base64url id and independent token, 1024 ceiling, forgotten `--head-timeout` after negotiate);
  `HubRecordReader`, `HubMessageJsonReader`, `HubMessageJsonWriter`, `HubHandshake`. 144 tests; the
  library has 100% line and 100% branch coverage.
- Choice: the hub path is a constructor argument (`SignalRHubPath.FromListenPath`), because
  `ListenUrl` carries no path; BL-450 passes the listen URL's path when it composes the server.
- Choice: until BL-449 serves the upgrade, `GET [endpoint-base]` is `426 Upgrade Required` with
  `Sec-WebSocket-Version: 13` written directly (ADR-0071's refusal for a missing upgrade), so the
  library does not reference `Surl.WebSocket` yet; BL-449 replaces it with `Surl.WebSocket`'s check.
- Choice: a negotiate body is discarded through `IHttpRequestBody.DiscardAsync`; one past
  `--max-filesize` is refused `413`, a malformed one `400`, both closing the connection. Bodies on
  other answers are left unread, which closes the connection after the answer.
- Choice: `access_token` and other query parameters decode `+` as a space, as form encoding and
  ASP.NET Core's query parser do; the first occurrence of a name wins.
- Choice: a handshake whose `protocol` is not a string is `The handshake request is invalid.`; a
  `version` that is a number but not the integer 1 is `Requested protocol is not available.`
- Choice: arguments, items and results are kept as the exact bytes the client sent
  (`HubJsonValue`), and written back with `WriteRawValue`; the writer uses relaxed escaping so a text
  such as `It didn't work!` is written as HubProtocol.md's example shows it.
- `Documentation/Conformance/Extended/SignalR.md` added to `touches` to replace this task's ID with the
  named tests (ADR-0119 "Consequences"); no task in Doing names it. Rows whose transport half is
  BL-449's say so.
- HubProtocol.md's JSON examples were read from dotnet/aspnetcore at
  `6aacb16a27a677d16278a9814ed04f871020854b`; no byte comes from the Curl port.
## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. SignalR server negotiates (versions 0 and 1) behind the bearer login and reads and writes every JSON hub message with its 0x1E record separator
