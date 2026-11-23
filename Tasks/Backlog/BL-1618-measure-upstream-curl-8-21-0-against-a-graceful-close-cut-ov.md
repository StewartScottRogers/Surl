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
completed:
---
# BL-1618 — Measure upstream curl 8.21.0 against a graceful-close cut over HTTP/1.1 and FTP RETR (Integration)

## Goal

Pinned upstream curl 8.21.0 exit code and bytes kept are measured for graceful and reset cuts.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). ADR-0188 decision 2 list of cases. Use Record-CurlExchange.ps1 and the pinned build only; never the Curl port. Integration category.

## Acceptance criteria

- [ ] Integration tests for HTTP/1.1 Content-Length, HTTP/1.1 chunked, FTP RETR, and the same over TLS, each with `close=graceful` and `close=reset`, pin curl exit code and bytes kept.
- [ ] If FTP graceful gives exit 56, the 426 fallback of ADR-0188 is applied (file or reuse a BL-1613 follow-up) and an amendment recorded.
- [ ] Results are written to ADR-0188 as an amendment; no Curl port output.

## Notes

## Log

- 2026-11-22: Created.
