---
id: BL-1221
title: Add ITlsLoginJudge, IConnection.UseTlsLoginJudge, KerberosLogin and IAuthenticationPolicy.JudgeKerberosLoginAsync (ADR-0165)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1214]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-060
created: 2026-11-15
completed:
---
# BL-1221 — Add ITlsLoginJudge, IConnection.UseTlsLoginJudge, KerberosLogin and IAuthenticationPolicy.JudgeKerberosLoginAsync (ADR-0165)

## Goal

The contracts ADR-0165 decisions 2 and 8 name exist, with defaults that change no existing server.

## Context

ADR-0165 (Documentation/Planning/Decisions) decides this. No wire byte changes; existing upstream curl 8.21.0 fixtures stay green.

## Acceptance criteria

- [ ] ITlsLoginJudge (ShouldRefuseAsync, Judged), IConnection.UseTlsLoginJudge default no-op, KerberosLogin record, IAuthenticationPolicy.JudgeKerberosLoginAsync default returning IsTokenAccepted; each documented, tests cover defaults, CLAUDE.md of the project updated.
- [ ] dotnet build is clean and the fast tests pass; the project stays at 100% coverage.

## Notes

## Log

- 2026-11-15: Created.
