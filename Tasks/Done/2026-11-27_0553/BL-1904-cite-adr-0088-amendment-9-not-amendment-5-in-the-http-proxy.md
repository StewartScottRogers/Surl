---
id: BL-1904
title: Cite ADR-0088 Amendment 9, not Amendment 5, in the HTTP proxy's HTTP/2 doc comments
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1904 — Cite ADR-0088 Amendment 9, not Amendment 5, in the HTTP proxy's HTTP/2 doc comments

## Goal

The XML doc comments on the HTTP proxy's HTTP/2 types and tests cite the ADR-0088 amendment that decides HTTP/2 to the
HTTPS proxy.

## Context

Found during BL-1897 on 2026-11-27. Comments only. No store, event or fault changes. ADR-0088 Amendment 5 settles how
`Surl.Cli` reads the proxy options. Amendment 9 (BL-941) is the one that has the HTTPS proxy offer and serve `h2`; its
first paragraph says its number was meant to be 5. These files still cite Amendment 5:

- Library: `Http2ProxyStreamHandler.cs`, `Http2ProxyTunnel.cs`, `HttpProxyExchange.Http2.cs`.
- Tests: `Http2ProxyStreamHandlerTests.cs`, `HttpProxyProtocolServerHttp2Tests.cs`.

## Acceptance criteria

- [x] No file in `Surl.Protocol.HttpProxy.UnitLibrary` or `Surl.Protocol.HttpProxy.UnitTests` cites ADR-0088
  Amendment 5 for HTTP/2 behaviour; each cites Amendment 9 or decision 6, as ADR-0088's text supports.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-27: Changed six doc comments from Amendment 5 to Amendment 9, keeping "decision 6" where it was cited:
  the five files named in Context plus `HttpProxyExchange.cs` ("Amendments 2 and 5" to "2 and 9", on the `h2`
  condition), which Context did not list.
- The library's `CLAUDE.md` "Amendments 5 to 8" and `Surl.Cli`'s "decision 7 and Amendment 5" are about the command
  line and composition, which those amendments do decide, so they stay.
- Made the edits directly instead of delegating to `align-and-document`: six one-word replacements.
- Verified: `dotnet build` 0 warnings; fast tests 0 failed; `dotnet format --verify-no-changes` clean on both projects.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The HTTP proxy's HTTP/2 doc comments cite ADR-0088 Amendment 9
