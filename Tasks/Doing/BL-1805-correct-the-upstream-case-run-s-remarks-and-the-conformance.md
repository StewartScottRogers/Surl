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
completed:
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

- [ ] The `<remarks>` of `UpstreamTestCasesAgainstSurlTests` say the certificates are generated from
      the imported `.prm` descriptions.
- [ ] Its `<summary>` names every server family in `ServerFamilies`.
- [ ] The conformance library's `CLAUDE.md` no longer presents as intent anything the repository
      already does.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
