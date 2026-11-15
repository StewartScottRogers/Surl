---
id: BL-1222
title: Make TlsSrpServer ask an ITlsSrpLoginJudge once the client Finished verifies and refuse as a wrong password (ADR-0165)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1214]
touches: [Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests]
requirement: FR-060
created: 2026-11-15
completed:
---
# BL-1222 — Make TlsSrpServer ask an ITlsSrpLoginJudge once the client Finished verifies and refuse as a wrong password (ADR-0165)

## Goal

TlsSrpServer takes an optional ITlsSrpLoginJudge and, once the client Finished verifies, asks ShouldRefuseAsync; a true answer fails as bad_record_mac exactly as a wrong password.

## Context

ADR-0165 (Documentation/Planning/Decisions) decides this. No wire byte changes; existing upstream curl 8.21.0 fixtures stay green.

## Acceptance criteria

- [ ] Judged is called with false on wrong password or unknown user and true on success; no call before Finished; bytes equal the existing wrong-password fixture; message "The TLS-SRP login failed: login refused for '<name>'."; CLAUDE.md updated.
- [ ] dotnet build is clean and the fast tests pass; the project stays at 100% coverage.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
