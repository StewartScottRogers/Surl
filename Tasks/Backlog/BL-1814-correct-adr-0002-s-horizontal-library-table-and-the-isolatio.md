---
id: BL-1814
title: Correct ADR-0002's horizontal library table and the isolation test's ADR list against the project files
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0002-mirror-the-curl-ports-project-map.md, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-26
completed:
---
# BL-1814 — Correct ADR-0002's horizontal library table and the isolation test's ADR list against the project files

## Goal

ADR-0002's horizontal library table and amendment notes, and the summary comment of `ProtocolIsolationTests`, agree with the project files and with the test's allow-list.

## Context

Found by BL-1811. Check each against the `ProjectReference` entries before editing:

- The table has no rows for `Surl.HttpServing`, `Surl.WebSocket`, `Surl.Dtls`, `Surl.Cryptography.Curve448` or `Surl.Cryptography.Sm2` (amendment notes only), and nothing about `Surl.Cookies`, which the test allows and `Surl.Protocol.Http` references (ADR-0189).
- The `Surl.Tls13` row lists only Curve25519 and MlKem; the project also references ChaCha20, Poly1305, Curve448 and Sm2.
- The ADR-0088 amendment note says the HTTP proxy references `Surl.Authentication`; its project file does not.
- The ADR-0057 amendment note says no protocol server needs Kerberos; `Surl.Protocol.Socks` references it.
- The ADR list in the summary comment of `Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` lacks ADR-0147 and ADR-0189.

## Acceptance criteria

- [ ] ADR-0002's table, with its amendments, has an entry for every horizontal library on the allow-list of `ProtocolIsolationTests`, each with the references its project file has.
- [ ] No ADR-0002 amendment note contradicts a current project file without saying when it stopped being true.
- [ ] The summary comment of `ProtocolIsolationTests` names ADR-0147 and ADR-0189, and no assertion in the test changes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
