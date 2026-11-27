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
completed:
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

- [ ] The conformance `CLAUDE.md` says `https` serves the certificate a case's `<server>` line
      names, else `test-localhost`, and names the client-certificate half.
- [ ] ADR-0080 has a dated amendment, marked "Decided by Claude under Stewart's delegation",
      saying which rows decision 2's table marks "not built" are built in `UpstreamTestCaseServers`.
- [ ] ADR-0080's header lists that amendment.

## Notes

## Log

- 2026-11-26: Created.
