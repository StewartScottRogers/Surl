---
id: BL-1836
title: Correct ADR-0002's horizontal library table and the isolation test's ADR list against the project files
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0002-mirror-the-curl-ports-project-map.md, Foundation/Surl.Protocol.Abstractions.UnitTests, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1836 — Correct ADR-0002's horizontal library table and the isolation test's ADR list against the project files

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

- [x] ADR-0002's table, with its amendments, has an entry for every horizontal library on the allow-list of `ProtocolIsolationTests`, each with the references its project file has.
- [x] No ADR-0002 amendment note contradicts a current project file without saying when it stopped being true.
- [x] The summary comment of `ProtocolIsolationTests` names ADR-0147 and ADR-0189, and no assertion in the test changes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered by `align-and-document`. A script comparing the ADR's table with the `ProjectReference` entries found 33 rows and 0 differences; the allow-list has the same libraries.
- Added rows for Cookies, Curve448, Sm2, Dtls, HttpServing and WebSocket, completed the Tls13 row, and added header entries for ADR-0157 and ADR-0189. Out-of-date amendment notes keep their wording, with a dated "Since then" or "Correction" sentence beside it, so the decision history is not rewritten.
- ADR-0157 was also missing from the test's summary comment (the Tls13 row relies on it), so it is named too. The summary on `ProtocolServers_IncludeTheSocksServer` now says Socks references only Abstractions and horizontal libraries.
- Removed the paragraph under the table that listed which tasks "create" each project, in the present tense. Every project exists; the amendment notes and ADR-0048 and ADR-0050 keep the history.
- Added `Documentation/Product/Product-Overview.md` to `touches`: it said ADR-0002's table has no Cookies row, which this task made false. No other task in `Doing` named it.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: no failures in any test project. `dotnet format --verify-no-changes` on the changed `.cs` file: exit 0.
- Follow-ups filed: BL-1850, BL-1851, BL-1852.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0002's table and amendment notes and the isolation test's ADR list agree with the project files
