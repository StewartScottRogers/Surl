---
id: BL-083
title: Correct HttpProtocolServer.Schemes' comment now that surl registers https in its composition
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-038]
touches: [Surl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-09-29
completed:
---
# BL-083 — Correct HttpProtocolServer.Schemes' comment now that surl registers https in its composition

## Goal

The XML doc comment on `HttpProtocolServer.Schemes` says where `https` is served from, as
ADR-0019 decided.

## Context

BL-038 registered `https` in `Surl.Console` through `ImplicitTlsSchemeServer` (ADR-0019)
while BL-050 held `Surl.Protocol.Http.UnitLibrary`. The comment on
`HttpProtocolServer.Schemes` (`Surl.Protocol.Http.UnitLibrary/HttpProtocolServer.cs`) still
says "`https` joins it with the TLS contract", which no longer says where that happens.
Doc comment only; no behaviour change.

## Acceptance criteria

- [ ] The comment on `HttpProtocolServer.Schemes` says it answers `http`, and that `surl`
      serves `https` with the same server through `ImplicitTlsSchemeServer` (ADR-0019).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-29: Created.
