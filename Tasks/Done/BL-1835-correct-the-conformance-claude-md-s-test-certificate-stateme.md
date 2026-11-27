---
id: BL-1835
title: Correct the conformance CLAUDE.md's test certificate statement and note in ADR-0080 that every server half is built
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md]
requirement: FR-053
created: 2026-11-26
completed: 2026-11-27
---
# BL-1835 — Correct the conformance CLAUDE.md's test certificate statement and note in ADR-0080 that every server half is built

## Goal

The conformance library's `CLAUDE.md` says which certificate each TLS server half serves, and
ADR-0080 says that the server halves its decision 2 table marks "not built" have since been built.

## Context

Found during BL-1808 (2026-11-26).

- The `CLAUDE.md` paragraph "The upstream test certificates are generated (BL-682)" says
  `UpstreamTestCasesAgainstSurlTests` serves `https` with `test-localhost`. A case's `<server>`
  line can name another certificate (`UpstreamTestCaseCertificateName`, BL-1130), with
  `test-localhost` for a case naming none, and an `https-mtls` half requires a client certificate
  chaining to `test-ca` (BL-1131).
- ADR-0080's decision 2 table marks the proxy and SOCKS, `http-unix`, `httptls+srp` and `dns` rows
  "not built". Every row of `UpstreamTestCaseServers` says built. Add a dated amendment; leave the
  decision text as it is.

Stand-in questions: this task changes prose only.

## Acceptance criteria

- [x] The conformance `CLAUDE.md` says `https` serves the certificate a case's `<server>` line
      names, else `test-localhost`, and names the client-certificate half.
- [x] ADR-0080 has a dated amendment, marked "Decided by Claude under Stewart's delegation",
      saying which rows decision 2's table marks "not built" are built in `UpstreamTestCaseServers`.
- [x] ADR-0080's header lists that amendment.

## Notes

- 2026-11-27: `align-and-document` made both edits. Every "not built" row is built, and so are the
  `http/2` and `http/3` rows; the amendment lists the server half the run composes for each.
- The title's "every server half is built" is not true of RTSP: the `rtsp` and `rtsp-ipv6` rows say
  built, but the run composes no server half for them. The amendment says that instead of the
  title's wording. All 9 cases naming an `rtsp` server are libtest programs (counted with
  PowerShell over the imported data).
- Stale statements outside this task's goal are filed as BL-1850 (XML comments) and BL-1851
  (ADR-0080's amendment list and rows).
- Verification: `dotnet build` 0 warnings, 0 errors; fast tests 30,198 passed, exit code 0.
  Only Markdown changed, so `dotnet format` was not run.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The conformance CLAUDE.md states each TLS server half's certificate, and ADR-0080 is amended with the server halves since built (rtsp has a built row but no composed half).
