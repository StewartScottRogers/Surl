---
id: BL-1803
title: Correct the stale conformance statements in the glossary, the conformance library's CLAUDE.md and the pass-rate file
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md, Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md, Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md]
requirement: FR-053
created: 2026-11-26
completed:
---
# BL-1803 — Correct the stale conformance statements in the glossary, the conformance library's CLAUDE.md and the pass-rate file

## Goal

The glossary, the conformance library's `CLAUDE.md` and the pass-rate file state which platforms,
server halves and upstream test cases the repository covers now, with older results written as
history.

## Context

Found by BL-1800. The statements below were verified on 2026-11-26; they need correcting, not
auditing again. Line numbers are approximate.

- `Documentation/Wiki/Glossary.md` line 342, "published-binary conformance", says CI runs it for
  `linux-x64` and `osx-arm64`. The `conformance` job matrix in `.github/workflows/release.yml` also
  has `linux-arm64`, `win-arm64` and `osx-x64`. `win-x64` still runs it locally.
- `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` about line 302 says `imaps`, `pop3s` and
  `smtps` are not composed. BL-1120 composed them: the IMAP, POP3 and SMTP case servers also listen
  behind implicit TLS on `127.0.0.1`, and `%IMAPSPORT`, `%POP3SPORT` and `%SMTPSPORT` are their ports.
- The same file about line 309 says case 1230 skips `server-half-not-composed:http-ipv6`. BL-1072
  composed that half (the plaintext HTTP half also binds `[::1]`, and `%HTTP6PORT` is its port), and
  1230 is in all three `Documentation/Conformance/upstream-test-cases-passing.<platform>.txt` files.
- `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` about lines 61 to 66 says in the
  present tense that 2402, 2404, 2407 and 2502 are recorded as failures. That was the `linux-x64`
  run at commit `6023b4b3` (BL-1317). 2402, 2404 and 2502 pass from commit `0cd51f95` (BL-1325) and
  2407 from commit `4ac7b2c7` (BL-1326), as the later notes and the "Current" table show. `win-x64`
  and `osx-arm64` still skip all four.

`README.md` and `Documentation/Product/Product-Overview.md` belong to BL-1799, and
`Documentation/Product/Requirements.md` and `Documentation/Planning/Roadmap.md` to BL-1800 and
BL-1802. No stand-in store, event or fault applies: this task changes prose only.

## Acceptance criteria

- [ ] The "published-binary conformance" row of `Documentation/Wiki/Glossary.md` says CI runs it for
      `linux-x64`, `linux-arm64`, `win-arm64`, `osx-x64` and `osx-arm64` (the `conformance` job's
      matrix in `.github/workflows/release.yml`) and that `win-x64` runs it locally.
- [ ] `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` no longer says `imaps`, `pop3s` and
      `smtps` are not composed, and says their case servers listen behind implicit TLS on
      `127.0.0.1` at `%IMAPSPORT`, `%POP3SPORT` and `%SMTPSPORT`.
- [ ] `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` no longer says case 1230 skips
      `server-half-not-composed:http-ipv6`.
- [ ] The pass-rate file's paragraph about 2402, 2404, 2407 and 2502 writes their failures in the
      past tense as the result at commit `6023b4b3`, says all four pass on `linux-x64` now (naming
      `0cd51f95` for 2402, 2404 and 2502 and `4ac7b2c7` for 2407), and still says `win-x64` and
      `osx-arm64` skip them. No table or `upstream-test-cases-passing.<platform>.txt` file changes.
- [ ] `dotnet test --filter "FullyQualifiedName~UpstreamTestCasePassRateStatementTests"` passes.

## Notes

## Log

- 2026-11-26: Created.
