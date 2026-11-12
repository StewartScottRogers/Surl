---
id: BL-1018
title: Serve DNS over HTTPS for --dns-transport https
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1017, BL-483, BL-902]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1018 — Serve DNS over HTTPS for --dns-transport https

## Goal

`surl --dns-transport https --self-signed dns://127.0.0.1:0` serves DNS over HTTPS (RFC 8484) at `/dns-query`
over HTTP/2 and HTTP/1.1, as ADR-0092 decision 5 says.

## Context

- Third of the four tasks BL-485 was split into (see BL-1032). It adds `https` to `--dns-transport`'s allowed
  values (BL-1017 added the option) and composes the listener.
- Decision: ADR-0092 decision 5, the DoH bullets: through `Surl.HttpServing`'s `IHttpRequestHandler` (HTTP/1.1)
  and `Surl.Http2`'s `IHttp2StreamHandler` (HTTP/2), one handler deciding both - `DnsOverHttpsHandler` in
  `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary` (BL-483) takes the zones and
  `allowZoneTransfer`. ALPN `h2` then `http/1.1` (ADR-0081 decision 2's order), default port 443. No DoH over
  HTTP/3 (ADR-0086 decision 6).
- Port sharing: `tcp` or `tls` with `https` on one explicit non-zero port is refused `FailedInit` (2) with
  `surl: --dns-transport <a> and <b> cannot share port <n>`. `https` needs `--cert` or `--self-signed`.
- Logins on DoH are BL-827's, which waits on this task.
- Stand-in: no store; the DoH exchanges are the engine's events.

## Acceptance criteria

- [ ] A named `Surl.Cli.UnitTests` test reads `--dns-transport udp,tcp,https`; the option's allowed values in its
      `--aihelp` facts list `https`.
- [ ] Named `Surl.Composition.UnitTests` tests show `https` composing a TLS listener offering `h2` then `http/1.1`
      whose requests reach `DnsOverHttpsHandler`, and `tls,https` on one explicit port refused with the decision's
      text.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass; `dotnet build -warnaserror` is clean, the
      fast tests are green, and the touched libraries keep 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 6): the plan needs more than the first `touches` named. Nothing on a `ListenUrl` says "this
  implicit-TLS `dns` listener is DoH", so `Surl.Networking`'s `TlsApplicationProtocols.ForListener` can only offer
  `dot` for it (it keys on `IsImplicitTls` + scheme `dns`). ADR-0092 Amendment 1 point 5 allows "one more init-only
  member naming the server" (additive). Plan, for the run after BL-902 is Done:
  1. `Surl.Protocol.Abstractions`: an init-only `ListenUrl` member (e.g. `IsDnsOverHttps`, default `false`, part of
     equality) - contract change, hence `Surl.Protocol.Abstractions.UnitLibrary`/`.UnitTests` in `touches`.
  2. `Surl.Networking`: `TlsApplicationProtocols.ForListener` offers `h2` then `http/1.1` for `{ Scheme: "dns", IsDnsOverHttps: true }`.
  3. `Surl.Composition`: `DnsTransportComposition` expands `https` to a `Connections`, `IsImplicitTls` listen URL with
     that member on port 443 by default; `FindRefusal` also refuses `tcp`/`tls` with `https` on one explicit port;
     a second `dns` connection server (or one wrapper choosing by the member / agreed ALPN) over
     `DnsOverHttpsHandler(zones, exchange, allowZoneTransfer)` through `Http1ConnectionServer` and, when ALPN is `h2`,
     `Http2ConnectionServer` (model: `HttpProtocolServer.ServeAsync`); `ServerTlsComposition` must treat it as TLS-first.
  4. `Surl.Cli`: `DnsTransportWords` gains `https`; `--aihelp`/manual text and the `AiHelpTextTests` refusal list.
  Blocked on BL-902, which touches `Foundation/Surl.Protocol.Abstractions.UnitLibrary`.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. needs Surl.Protocol.Abstractions (a ListenUrl member so Surl.Networking can offer h2 then http/1.1 for DoH) which BL-902 touches; and Surl.Networking
- 2026-11-12: Backlog -> Doing.
