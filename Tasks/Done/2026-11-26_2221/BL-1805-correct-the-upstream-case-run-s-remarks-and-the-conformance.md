---
id: BL-1805
title: Correct the upstream-case run's remarks and the conformance CLAUDE.md's intent list
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamTestCasesAgainstSurlTests.cs, Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md]
requirement: FR-053
created: 2026-11-26
completed: 2026-11-26
---
# BL-1805 — Correct the upstream-case run's remarks and the conformance CLAUDE.md's intent list

## Goal

The doc comments of `UpstreamTestCasesAgainstSurlTests` and the "The rest is intent" paragraph of the conformance library's `CLAUDE.md` say what the code does now.

## Context

Found during BL-1803 (2026-11-26).

- `UpstreamTestCasesAgainstSurlTests.cs`, `<remarks>`: says the imported `.prm` descriptions "are not
  read yet". `WriteCertificates` in the same file reads `certs/*.prm` and passes them to
  `UpstreamTestCertificateGenerator.Generate` (BL-682).
- The same file's `<summary>` lists HTTP, FTP, Gopher, MQTT, TFTP, DICT, TELNET, SCP, SFTP and SMB.
  `ServerFamilies` also composes the mail, HTTP proxy, SOCKS, TLS-SRP and DNS halves.
- `Surl.Conformance.UnitLibrary/CLAUDE.md`, "The rest is intent until its task lands": names each
  server's port variable beside its `UpstreamTestCaseServers` row, and the SSH keys with
  `%SSHSRVMD5`/`%SSHSRVSHA256`. Only the proxy, SOCKS, TLS-SRP and DNS rows carry a `PortVariable`
  (the tests map the other ports by name), and `UpstreamTestCaseFileServers` in the tests makes the
  SSH keys and hashes. Say which of this is built and where, and keep as intent only what is still
  planned.

## Acceptance criteria

- [x] The `<remarks>` of `UpstreamTestCasesAgainstSurlTests` say the certificates are generated from
      the imported `.prm` descriptions.
- [x] Its `<summary>` names every server family in `ServerFamilies`.
- [x] The conformance library's `CLAUDE.md` no longer presents as intent anything the repository
      already does.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The `<remarks>` and `<summary>` now describe the generated certificates, every server family, the
  Debug build (ADR-0173), the libtests binary (ADR-0174) and the tolerated skips. Four stale
  comments in the same file were corrected too (port-variable helper, `CaseRun`, `PairedBuilds`,
  the Unix socket folder and upload comments). Comments only; no code changed.
- Choice (decided by Claude): the conformance `CLAUDE.md` keeps no intent list. No open task plans to
  move the port variables or the SSH keys into the library, so the paragraph states where each is
  built today. No ADR here: nothing about behaviour or design changes; amending ADR-0080's wording
  is outside `touches` and is filed as BL-1808.
- The DNS paragraph's "runs for real if a debug build is ever pinned" was removed: a Debug build is
  pinned, and no pin is given `override-dns`.
- The edits were made in the session instead of through `align-and-document`: the check of the code
  had already been done, so a fresh agent would only have repeated it.
- Verify: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast
  tests all passed. Integration tests not run: the diff is comments and Markdown.
- Follow-up: BL-1808.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The upstream-case run's doc comments and the conformance CLAUDE.md now describe the code as built
