---
id: BL-1618
title: Measure upstream curl 8.21.0 against a graceful-close cut over HTTP/1.1 and FTP RETR (Integration)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1608, BL-1612, BL-1613, BL-1614]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: FR-062
created: 2026-11-22
completed: 2026-11-23
---
# BL-1618 — Measure upstream curl 8.21.0 against a graceful-close cut over HTTP/1.1 and FTP RETR (Integration)

## Goal

Pinned upstream curl 8.21.0 exit code and bytes kept are measured for graceful and reset cuts.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). ADR-0188 decision 2 list of cases. Use Record-CurlExchange.ps1 and the pinned build only; never the Curl port. Integration category.

## Acceptance criteria

- [x] Integration tests for HTTP/1.1 Content-Length, HTTP/1.1 chunked, FTP RETR, and the same over TLS, each with `close=graceful` and `close=reset`, pin curl exit code and bytes kept.
- [x] If FTP graceful gives exit 56, the 426 fallback of ADR-0188 is applied (file or reuse a BL-1613 follow-up) and an amendment recorded.
- [x] Results are written to ADR-0188 as an amendment; no Curl port output.

## Notes

Measured 2026-11-23: graceful cut exits 18, reset exits 56, for HTTP Content-Length, FTP RETR and both over TLS; chunked
(loopback server, graceful) exits 18. FTP graceful gives 18, so the 426 fallback is not needed. Recorded as ADR-0188
amendment 1. Chunked is served by a loopback server in the test because surl's file server never answers chunked.

## Log

- 2026-11-22: Created.
- 2026-11-23: Backlog -> Doing.
- 2026-11-23: Doing -> Done. Curl 8.21.0 exits 18 on a graceful cut and 56 on a reset (HTTP, FTP, TLS); ADR-0188 amendment 1, no 426 fallback needed
