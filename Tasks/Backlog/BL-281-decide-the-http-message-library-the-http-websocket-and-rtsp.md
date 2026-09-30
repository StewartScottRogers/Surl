---
id: BL-281
title: Decide the HTTP message library the HTTP, WebSocket and RTSP servers share
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md]
requirement: FR-048
created: 2026-09-30
completed:
---
# BL-281 — Decide the HTTP message library the HTTP, WebSocket and RTSP servers share

## Goal

An accepted ADR decides where the HTTP/1.x message machinery lives that `Surl.Protocol.Http`,
`Surl.Protocol.Ws` (the upgrade request) and `Surl.Protocol.Rtsp` (RTSP/1.0 is HTTP/1.1's message
syntax) all need, and adds the horizontal library that holds it to ADR-0002's reference table, so
the WebSocket and RTSP servers can be built without referencing `Surl.Protocol.Http`.

## Context

- Protocol servers never reference each other (ADR-0002 decision 3, root `CLAUDE.md`), so what
  they share lives in a horizontal library that joins ADR-0002's table through a new ADR - the
  precedent is ADR-0050, which added `Surl.MailStore` and `Surl.LineProtocol` for the mail servers.
- What exists today, all internal to `Surl.Protocol.Http.UnitLibrary`: `HttpConnectionReader`,
  `HttpRequestHeadLineReader`, `HttpRequestHeadReadOutcome`/`HttpRequestHeadReadResult` (the
  request head read bounded by `ExchangeLimits` `--max-request-head` and the head timeout,
  ADR-0006 and ADR-0019), `HttpRequestLineParser`, `HttpFieldLineParser`, `HttpRequestField`,
  `HttpRequestHead`, `HttpResponseHead`, `HttpSyntax`, `HttpStatus`, `HttpRequestBodyFraming`, and
  the `WWW-Authenticate` writing in `HttpRequestResponder` (one field per value
  `IHttpAuthenticationSession.JudgeAsync` gives, ADR-0032 section 6). The glossary's "request
  head", "request line" and "field line" rows point at these types.
- What the other two servers need from it:
  - WebSocket (RFC 6455 section 4.2.1): read a bounded `GET` request head with `Upgrade`,
    `Connection`, `Sec-WebSocket-Key`, `Sec-WebSocket-Version`, `Host` and `Authorization`
    fields; write a `101 Switching Protocols` head or a refusal head; answer HTTP challenges
    through `IHttpAuthenticationSession`.
  - RTSP (RFC 2326 sections 4, 6, 7): request line `METHOD rtsp://... RTSP/1.0`, field lines,
    a `Content-Length` body, response head `RTSP/1.0 <code> <reason>`, `CSeq` echoed, the same
    `WWW-Authenticate` challenges (RFC 2326 section 11 uses HTTP's authentication), bounded by
    `--max-request-head` (ADR-0006's row already names RTSP).
- **Planning assumption, for the ADR to adopt or overturn:** a new horizontal library
  `Surl.HttpMessage.UnitLibrary` (referencing `Surl.Protocol.Abstractions` only) holds the
  bounded request-head reader, the request-line parser generalised to the protocol name and
  version it is given (`HTTP/1.0`, `HTTP/1.1`, `RTSP/1.0`), field-line parsing, response-head
  writing and the challenge-field writing; `Surl.Protocol.Http`, `Surl.Protocol.Ws` and
  `Surl.Protocol.Rtsp` reference it. The tasks after this one are filed on that assumption: the
  scaffold (creates the projects), the move out of `Surl.Protocol.Http`, the WebSocket and RTSP
  decision tasks and their servers. If the ADR decides otherwise (for example each server keeps
  its own reader), it says why, and the task run has `task-planner` re-plan those tasks (the
  scaffold and move tasks go to `Deferred` with the ADR as the reason).
- Decide also: the library's exact contents and public surface (what stays in
  `Surl.Protocol.Http` - method dispatch, content responses, chunked bodies, 100-continue per
  ADR-0027), the glossary rows' new code column, and whether the WebSocket upgrade is answered only
  on `ws://`/`wss://` listen URLs or also on `http://` listeners (a contract in Abstractions if so;
  file it as its own task if decided yes).
- No upstream curl measurement is needed here: the ADR pins no byte; RFC 9112, RFC 6455 and
  RFC 2326 give the syntax, and the WebSocket and RTSP decision tasks measure what curl sends.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude under
      Stewart's delegation", decides every point in Context: the library's name, contents,
      references and who references it.
- [ ] `ADR-0002-mirror-the-curl-ports-project-map.md` carries an "Amended" line naming the new ADR.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR.
- [ ] `Documentation/Product/Product-Overview.md` "Layers" and "Project layout" list the library,
      written as intent until the scaffold task creates it.

## Notes

- Filed as the first Phase 4 and 5 decision because it unblocks both the WebSocket and the RTSP
  chains.

## Log

- 2026-09-30: Created.
