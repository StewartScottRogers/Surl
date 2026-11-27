---
id: BL-1889
title: Correct the Conformance CLAUDE.md introduction that says the library plays the server half
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1889 — Correct the Conformance CLAUDE.md introduction that says the library plays the server half

## Goal

The introduction of `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` says what the library does with upstream
curl's test cases and where the server halves are.

## Context

Found during BL-1849 (checked on 2026-11-27). Docs only.

- Lines 5-10 call the library "The server half of upstream curl's own test suite" and say it "plays each case's
  replies from Surl".
- Near line 415 the same file says "It does not hold the server halves: each protocol library carries its own
  optional seam for a case's reply, and `Surl.Conformance.UnitTests` composes it". The csproj references only
  `Surl.Protocol.Abstractions.UnitLibrary`, so the later text is the true one.
- Not every server half is a protocol library's seam: DICT, SCP, SFTP and SMB cases run against Surl's ordinary
  servers, and the TLS-SRP half is `UpstreamTestCaseTlsSrpServerHalf` in the test project
  (`UpstreamTestCaseServerHalves`, `UpstreamTestCaseFileServers`). BL-1849 words the README and the product
  overview this way.
- ADR-0080 and its 2026-11-26 amendment decide where the parts live.
- BL-1855 corrects the `.cs` doc comments; leave those to it.

## Acceptance criteria

- [ ] The introduction no longer says the library is the server half or plays a case's replies.
- [ ] It says the library parses the cases and checks a case's verify section, and that the server halves are
      composed by `Surl.Conformance.UnitTests`.
- [ ] It says where the server halves come from (protocol library seams, Surl's ordinary servers, the test
      project's TLS-SRP half), or points to the later paragraph that does, which then says all three.
- [ ] Only that `CLAUDE.md` changes.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
