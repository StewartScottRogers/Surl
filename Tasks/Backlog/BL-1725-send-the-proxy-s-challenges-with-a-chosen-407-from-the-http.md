---
id: BL-1725
title: Send the proxy's challenges with a chosen 407 from the HTTP proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1700]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0160-how-the-http-and-https-proxy-applies-delay-drop-and-chosen-error-fault-rules.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1725 — Send the proxy's challenges with a chosen 407 from the HTTP proxy

## Goal

A fault rule that makes the HTTP or HTTPS proxy answer `407` sends the `Proxy-Authenticate` challenges the proxy's
own `407` would offer on that connection, over HTTP/1.1 and HTTP/2.

## Context

- Found during BL-1700. Verified in code on 2026-11-25: in `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary`,
  `HttpProxyExchange.cs` `AnswerCommandFaultAsync` and `Http2ProxyStreamHandler.cs` `EndsStreamForFaultAsync` call
  `AnswerAsync` with the chosen status and no challenges.
- RFC 9110 section 15.5.8: a proxy sending `407` MUST send `Proxy-Authenticate`; section 11.7.1: at least one in
  each `407` it generates.
- ADR-0160 decision 3 deliberately says "A chosen `407` carries no `Proxy-Authenticate` challenge", and
  `HttpProxyCommandFaultTests.ServeAsync_ConnectChosen407_CarriesNoProxyAuthenticateChallenge` pins it. This task
  replaces that choice by an amendment to ADR-0160 (it already has Amendment 1).
- Decision (Claude under Stewart's delegation; recorded by this task):
  1. A chosen status `407`, whatever its reason phrase, carries the values of the connection's proxy login session's
     `CreateChallenges()`, one `Proxy-Authenticate` field per value, in order: to `CONNECT`, to a forwarded request
     and on an HTTP/2 stream.
  2. When the session offers none (the contract's default, as `AnonymousAuthenticationPolicy`'s session), the `407`
     goes without the field and the exchange log notes
     `fault rule <id> chose 407, and the proxy offers no challenge on this connection`.
  3. Over HTTP/2, NTLM and Negotiate challenges, even when they are the only ones offered, are sent like any other:
     no stream is filtered or reset. This is what the proxy's own login `407` does over HTTP/2 today
     (`Http2ProxyStreamHandler.RefuseProxyLoginAsync` sends every verdict value), so a chosen `407` matches it. It
     differs from the HTTP server, which resets such a `401` with `HTTP_1_1_REQUIRED` (ADR-0081 decision 7,
     ADR-0134 Amendment 1); the amendment states the difference and that the proxy's login answers are not changed.
     No claim is made about what upstream curl does with them over HTTP/2: it has not been measured.
  4. Other chosen statuses are unchanged, and a chosen `401` is not given `WWW-Authenticate`. Whether the connection
     closes after the answer is unchanged.
- On HTTP/2, streams share one login session; call `CreateChallenges()` under the same `loginGate` as `JudgeAsync`.
- `Protocols/Http/Surl.Protocol.HttpProxy.UnitTests/UnitTestProxyAuthenticationPolicy.cs`'s session needs to
  implement `CreateChallenges`.
- No new recording from pinned upstream curl 8.21.0 is needed: the field has the shape of the proxy's login `407`,
  already pinned by the fixtures ADR-0088 Amendment 3 describes.
- Stand-in: no store changes; the rule publishes the same one `FaultAppliedEvent`, and a test seeds the fault with
  `error;scheme=http-proxy;reply=407` (or `https-proxy`) and sees the challenges in the response.

## Acceptance criteria

- [ ] Named tests in `HttpProxyCommandFaultTests` show a chosen `407` to a `CONNECT` and to a forwarded `GET` carries
      the session's challenges as `Proxy-Authenticate` fields, in order, and no origin is contacted.
- [ ] A named test shows a chosen `407` on an HTTP/2 stream carries them as `proxy-authenticate` fields, with an
      NTLM and Negotiate only list sent whole and no stream reset.
- [ ] Named tests show that a session offering none gives a `407` with no `Proxy-Authenticate` and the note of
      decision 2, and that a chosen `503` and `401` carry neither `Proxy-Authenticate` nor `WWW-Authenticate`.
- [ ] ADR-0160 has an amendment naming this task that states decisions 1 to 4, cites RFC 9110 sections 11.7.1 and
      15.5.8 and says which sentence of decision 3 it replaces; its row in
      `Documentation/Planning/Decisions/README.md` agrees.
- [ ] The library's `CLAUDE.md` and the doc comments describing a chosen error say what a chosen `407` sends.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary -warnaserror` is clean, the fast tests are
      green with no `TestCategory=Integration` test needed, and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
