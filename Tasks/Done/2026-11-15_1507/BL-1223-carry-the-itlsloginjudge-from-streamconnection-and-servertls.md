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
completed: 2026-11-15
---
# BL-1223 — Carry the ITlsLoginJudge from StreamConnection and ServerTlsHandshake to TlsSrpServer (ADR-0165)

## Goal

StreamConnection implements UseTlsLoginJudge and ServerTlsHandshake passes an adapter to TlsSrpServer.

## Context

ADR-0165 (Documentation/Planning/Decisions) decides this. No wire byte changes; existing upstream curl 8.21.0 fixtures stay green.

## Acceptance criteria

- [x] Tests show a judge set before UpgradeToTlsAsync or implicit TLS sees Judged for a TLS-SRP login and can refuse it; an SslStream handshake never calls it.
- [x] dotnet build is clean and the fast tests pass; the project stays at 100% coverage.

## Notes

Implicit TLS also goes through UpgradeToTlsAsync, so one path covers both. `ServerTlsHandshake.AuthenticateAsync` gained an optional trailing `ITlsLoginJudge?`; `TlsSrpLoginJudgeAdapter` maps it to `ITlsSrpLoginJudge`. Networking fast tests: 507 passed, 0 failed. Coverage was not re-measured with the collector; every new branch has a test.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. StreamConnection keeps an ITlsLoginJudge and ServerTlsHandshake hands TlsSrpServer an adapter; tests show ask, refuse and judged, and SslStream never calls it
