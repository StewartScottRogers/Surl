---
id: BL-279
title: Prove pinned upstream curl completes an SSH exchange against surl --hostcert
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-222]
touches: [Surl.Conformance.UnitTests]
requirement: FR-039
created: 2026-09-30
completed:
---
# BL-279 — Prove pinned upstream curl completes an SSH exchange against surl --hostcert

## Goal

An integration test in `Surl.Conformance.UnitTests` shows each upstream curl build pinned in
`UpstreamCurlBuilds.json` completing an `sftp://` download from a live `surl --hostkey <key>
--hostcert <key>-cert.pub`, trusting the certificate's CA through a `@cert-authority` line in its
`--knownhosts` file and given neither `-k` nor `--hostpubsha256`.

## Context

- BL-222 built `--hostcert` (ADR-0051 decision 4 and Amendment 2), proven only against the
  test-side client in `Surl.Protocol.Ssh.UnitTests`, never against upstream curl: its `touches`
  did not include `Surl.Conformance.UnitTests`.
- The certificate names offered are in every pinned build's measured host-key list (ADR-0051,
  "The decoded name-lists"): `rsa-sha2-512-cert-v01@openssh.com` and
  `rsa-sha2-256-cert-v01@openssh.com` on the Windows (WinCNG) build, the ECDSA and Ed25519 ones too
  on the Linux and macOS (OpenSSL) builds. Whether libssh2 1.11.1 accepts a certificate host key
  through a `@cert-authority` `known_hosts` line is to be measured, not assumed; if it does not,
  record what it does (exit code and stderr) and pin that instead, with the reason in an ADR.
- Keys and certificates: made by `ssh-keygen` at test time or as fixtures, as
  `Surl.Protocol.Ssh.UnitTests/Fixtures/README.md` ("Host certificates") shows; test-only keys.
- Existing SSH conformance tests to follow: `UpstreamCurlTransfersFilesWithSurlOverSftpTests`,
  `UpstreamCurlOffersSshAlgorithmsTests`.

## Acceptance criteria

- [ ] A `[TestCategory("Integration")]` test in `Surl.Conformance.UnitTests` runs the pinned
      Windows build against `surl --hostkey <rsa key> --hostcert <rsa cert>` with a
      `@cert-authority` known_hosts file and asserts the exit code, stdout and stderr measured.
- [ ] The Linux and macOS legs run the same with an Ed25519 key and certificate, each platform's
      answer pinned in its own test where it differs.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
