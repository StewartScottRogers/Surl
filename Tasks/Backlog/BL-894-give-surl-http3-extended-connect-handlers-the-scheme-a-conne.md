---
id: BL-894
title: Give Surl.Http3 extended CONNECT handlers the scheme, a connection close, early datagrams and a drain before GOAWAY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-459]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-894 — Give Surl.Http3 extended CONNECT handlers the scheme, a connection close, early datagrams and a drain before GOAWAY

## Goal

An `IHttp3ExtendedConnectHandler` can do four things ADR-0117 asks of the WebTransport server and BL-459
found `Surl.Http3` does not offer: read the CONNECT's `:scheme`, close the whole connection with an error
code of its choosing, receive HTTP datagrams that arrived before their CONNECT, and send capsules on its
sessions at shutdown before `GOAWAY` and keep serving them through the shutdown grace.

## Context

- ADR-0117 decision 0 (`WT_REQUIREMENTS_NOT_MET` closes the connection), decision 1 (`:scheme` not `https`
  is `:status 400`), decision 3 (up to 64 datagrams a connection held for a session not yet established,
  for at most `--head-timeout`), decision 6 (`WT_DRAIN_SESSION`, then `GOAWAY`, sessions served during the
  grace, then `WT_CLOSE_SESSION` code 0; wait for the client to close each CONNECT stream before
  `CONNECTION_CLOSE`).
- Today: `Http3RequestHead` drops `:scheme`; `Http3ConnectionException` is internal, so a handler cannot
  close the connection; `Http3ExtendedConnect.Deliver` drops a datagram for no open session; and on
  cancellation `Http3ConnectionServer` cancels every handler at once and sends `GOAWAY` only after they
  return (`ServeAsync`, `EndAsync`).
- Keep the HTTP server's bytes unchanged: everything new is opt-in for extended CONNECT handlers.

## Acceptance criteria

- [ ] `Http3RequestHead` carries `:scheme` for requests that have one; a named test reads it on an extended CONNECT.
- [ ] A handler can close the connection with a code it chooses (e.g. on `Http3ExtendedConnectSession`); a named test sees `CONNECTION_CLOSE` with that code.
- [ ] A datagram whose quarter stream ID names a client bidirectional stream with no CONNECT yet is held (64 a connection, at most `--head-timeout`) and delivered when the session opens; a named test shows it delivered, another shows it dropped past the limit or the timeout.
- [ ] On shutdown a handler learns of it before `GOAWAY` is sent (a shutdown token or callback distinct from the connection's end), may still write to its session during the grace, and `GOAWAY` follows; named tests show the order.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, `Surl.Protocol.Http.UnitTests` still passes unchanged, and the library keeps 100% line and branch coverage.

## Notes

Filed by BL-459, which built the WebTransport server on what `Surl.Http3` offers today; BL-895 then uses these.

## Log

- 2026-11-05: Created.
