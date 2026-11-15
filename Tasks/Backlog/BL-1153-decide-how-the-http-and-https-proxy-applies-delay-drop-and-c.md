---
id: BL-1153
title: Decide how the HTTP and HTTPS proxy applies delay, drop and chosen-error fault rules
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1153 — Decide how the HTTP and HTTPS proxy applies delay, drop and chosen-error fault rules

## Goal

A new ADR, marked "Decided by Claude under Stewart's delegation", says how the HTTP and HTTPS proxy (`-x`/`--proxy` listeners) applies `delay`, `drop` and `error` fault rules at the command stage, so BL-1173 can wire it without a question.

## Context

- Stand-in audit 2026-11-14: `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary` never asks `ExchangeContext.Faults` - no `FaultPoint`, no `CommandStageFaults`, no `FaultAppliedEvent`. Accept-stage delay and drop reach it through the serving engine, and refuse-login through the shared policy, but a test cannot make the proxy answer a `CONNECT` with a 5xx, hold a forwarded request, or cut a tunnel. A proxy is one of the things Surl stands in for.
- The open question is matching: a proxy listener's listen URL has scheme `http` or `https`, the same schemes the HTTP server uses, so an `error;scheme=http;reply=503` rule would match both. ADR-0124 decision 4 lets a rule name `listener` instead. Decide what `FaultPoint.Scheme`, `Command` (the method, `CONNECT` included) and `Path` (the request target, or `host:port` for `CONNECT`) carry at the proxy, and whether a `scheme` rule reaches the proxy at all.
- Precedents: ADR-0088 (how Surl serves the CONNECT proxy and the HTTPS proxy), ADR-0134 (HTTP, RTSP and WebSocket chosen errors), ADR-0141 (command-stage delay and drop through `CommandStageFaults.FindAndApplyAsync`), BL-1061 (SOCKS faults, an original protocol whose curl exit codes come from pinned upstream curl).
- Upstream curl is the only oracle (ADR-0003): reading upstream's documentation is enough here; BL-1174 measures pinned upstream curl 8.21.0 after the wiring. Cite https://curl.se/libcurl/c/libcurl-errors.html for the exit codes expected (for example 56, `CURLE_RECV_ERROR`, when a `CONNECT` is answered non-2xx) and state that 8.21.0 is the version read.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` (next free number) decides the `FaultPoint` the proxy asks at for `CONNECT` and for a forwarded request (stage, scheme, command, path, account).
- [ ] It decides how a `scheme` rule and a `listener` rule each reach or miss the proxy, so an `error;scheme=http` rule written for the HTTP server never silently changes a proxy (or says why it should).
- [ ] It decides the chosen-error shape and where it is sent: the status and reason answering `CONNECT`, and the answer to a forwarded request, with whether `FaultReplyShapes` needs a new row.
- [ ] It decides what delay and drop do before and after a tunnel is established.
- [ ] It names the upstream curl 8.21.0 exit code it expects for each case, from upstream's documentation, for BL-1174 to confirm by measurement.
- [ ] ADR-0124's decision 4 table gains a row or an "Amended by" line pointing at the new ADR.

## Notes

## Log

- 2026-11-14: Created.
