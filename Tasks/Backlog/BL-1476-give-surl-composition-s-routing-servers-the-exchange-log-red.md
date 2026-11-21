---
id: BL-1476
title: Give Surl.Composition's routing servers the exchange-log redactor of the server they route to, so --proxy logs no Proxy-Authorization
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1476 — Give Surl.Composition's routing servers the exchange-log redactor of the server they route to, so --proxy logs no Proxy-Authorization

## Goal

With `--proxy` (or a SignalR hub) composed, the `-v` log, trace dumps and `--log-file` show
`Proxy-Authorization: Basic ****` and `Authorization: Basic ****` again: every server the serving engine
is handed redacts as the server behind it does (ADR-0182).

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md`
  item 4: a refused Basic proxy login still logs `Proxy-Authorization: Basic <base64>` under `-v`. BL-1425
  made `HttpProxyProtocolServer` redact (`Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary/HttpProxyProtocolServer.cs:62`,
  `:115-116`, `CreateRedactor` returns `HttpRequestHeadBytesRedactor`) and tested the server alone.
- The cause is in composition, not in the proxy's refusal path: `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:893`
  asks `server as IExchangeBytesRedactingServer` of the server it was handed, and with `--proxy`
  `ProxyComposition.RouteProxyListeners` (`Application/Surl.Composition.UnitLibrary/ProxyComposition.cs:55-56`) hands
  it a `ProxyRoutingProtocolServer`, which implements only `IConnectionProtocolServer, IConnectionRefusalWriter`
  (`Application/Surl.Composition.UnitLibrary/ProxyRoutingProtocolServer.cs:15`). So no redactor is made at all,
  for every login, accepted or refused, and the engine's `DecryptedBytes` sink (same method, `:906-907`) gets none
  either. It also wraps every server answering `http` or `https`, so with `--proxy` given the HTTP origin
  listeners and the `https` `ImplicitTlsSchemeServer` lose their redaction too.
- Checked the other wrappers in `Application/Surl.Composition.UnitLibrary`: `SignalRHubRoutingProtocolServer.cs:15`
  has the same gap (it routes to `SignalRProtocolServer`, which redacts, `SignalRProtocolServer.cs:60`).
  `ImplicitTlsSchemeServer` (`:58-59`) and `DnsConnectionRoutingProtocolServer` (`:39`) already forward or redact.
  `SingleSchemeServer` wraps SSH, which ADR-0182 decision 8 leaves unredacted (logged below its encryption);
  `ControlProtocolServer` logs nothing; `McpStdioListenUrlServer` is never served by the engine.
- `ExchangeRedactionContext` (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/ExchangeRedactionContext.cs`) carries
  only the exchange ID and scheme, not `ListenUrl.IsProxy`, so the router cannot ask the proxy or the origin by
  listen URL. Do not change that shared contract here. The HTTP server's `HttpConnectionBytesRedactor`
  (`Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpConnectionBytesRedactor.cs`) is the proxy's
  `HttpRequestHeadBytesRedactor` plus the HTTP/2 switch, so it masks both header fields on either kind of listen
  URL: return the origin server's redactor when it redacts, else the proxy server's, else a pass-through.
  If that turns out wrong for some proxy case, stop and file a task to add the listen URL to
  `ExchangeRedactionContext` (a contract change), rather than widening this one.
- Stand-in: no store, event or fault changes. The event stream keeps the wire bytes (ADR-0182); only the
  level's log, trace dumps and log file are redacted.

## Acceptance criteria

- [ ] `ProxyRoutingProtocolServer` and `SignalRHubRoutingProtocolServer` implement `IExchangeBytesRedactingServer`, and
      `ProxyRoutingProtocolServerTests` has named tests: origin redacting gives the origin's redactor, origin not
      redacting gives the proxy's, neither gives one that passes bytes through unchanged.
- [ ] A named test in `Application/Surl.Composition.UnitTests` starts a composed instance with `--proxy` and a proxy account
      (`-U alice:secret`) over `FakeListenerFactory`/`StartTestSeams`, sends a proxy `GET` with a wrong
      `Proxy-Authorization: Basic` and one with the right one, and finds `Proxy-Authorization: Basic ****` and no
      base64 credential in the `-v` log for both, and the 407 still sent.
- [ ] A named test shows an origin `http` listen URL beside `--proxy` logs `Authorization: Basic ****`.
- [ ] A named test composes every original and extended registration with a proxy listen URL and asserts that each
      composed server whose wrapped server is an `IExchangeBytesRedactingServer` is one itself, so a new wrapper
      cannot drop redaction silently.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"`
      is green, and coverage stays at 100% line and branch for `Surl.Composition.UnitLibrary`.

## Notes

- ADR-0182 needs no amendment: it already says every protocol server's received bytes are redacted; this restores it.

## Log

- 2026-11-20: Created.
