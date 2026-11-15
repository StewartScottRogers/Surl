---
id: BL-1223
title: Carry the ITlsLoginJudge from StreamConnection and ServerTlsHandshake to TlsSrpServer (ADR-0165)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1214, BL-1221, BL-1222]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: FR-060
created: 2026-11-15
completed:
---
# BL-1223 — Carry the ITlsLoginJudge from StreamConnection and ServerTlsHandshake to TlsSrpServer (ADR-0165)

## Goal

StreamConnection implements UseTlsLoginJudge and ServerTlsHandshake passes an adapter to TlsSrpServer.

## Context

ADR-0165 (Documentation/Planning/Decisions) decides this. No wire byte changes; existing upstream curl 8.21.0 fixtures stay green.

## Acceptance criteria

- [ ] Tests show a judge set before UpgradeToTlsAsync or implicit TLS sees Judged for a TLS-SRP login and can refuse it; an SslStream handshake never calls it.
- [ ] dotnet build is clean and the fast tests pass; the project stays at 100% coverage.

## Notes

## Log

- 2026-11-15: Created.
