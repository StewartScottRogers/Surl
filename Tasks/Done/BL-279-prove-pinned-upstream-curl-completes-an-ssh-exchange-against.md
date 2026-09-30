---
id: BL-279
title: Prove pinned upstream curl completes an SSH exchange against surl --hostcert
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-222]
touches: [Surl.Conformance.UnitTests, Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
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

- [x] A `[TestCategory("Integration")]` test in `Surl.Conformance.UnitTests` runs the pinned
      Windows build against `surl --hostkey <rsa key> --hostcert <rsa cert>` with a
      `@cert-authority` known_hosts file and asserts the exit code, stdout and stderr measured.
- [x] The Linux and macOS legs run the same with an Ed25519 key and certificate, each platform's
      answer pinned in its own test where it differs.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Measured, not assumed (ADR-0068): upstream curl 8.21.0 does **not** complete the exchange. libssh2
  1.11.1 lists each plain host-key name before its certificate name, so it agrees surl's plain key,
  and it reads no `@cert-authority` line, so curl logs `did not find host`, `host check 2` and exits
  60 `SSL peer certificate or SSH remote key was not OK`. Same on the WinCNG reference pin (RSA) and
  the OpenSSL static-curl Windows pin (Ed25519 and RSA), whatever the host pattern (`[127.0.0.1]:port`,
  `*`, `[localhost]:port` with a `localhost` URL). That refusal is what the tests pin.
- `UpstreamCurlTrustsSurlsHostCertificateTests`: RSA on the Windows reference pin; Ed25519 on the
  supplementary OpenSSL Windows pin (ADR-0063's precedent, same curl/libssh2/OpenSSL as the Linux and
  macOS pins); Ed25519 on the Linux and macOS reference pins, pinned to the OpenSSL Windows pin's
  answer and measured by CI; plus a control that `--hostpubsha256` of the plain key still downloads
  with `--hostcert` given.
- Keys and certificates: `SshTestHostCertificates`, copies of `Surl.Protocol.Ssh.UnitTests/Fixtures/host-certificates`
  (the conformance project does not reference that test project), rather than running `ssh-keygen`
  at test time, so no test depends on an OpenSSH install.
- `touches` widened with `Documentation/Planning/Decisions` for ADR-0068 and its index row; no task in
  `Doing` named it.
- surl's offer order is unchanged (ADR-0068 decision 2); no follow-up task filed.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned upstream curl's measured answer to surl --hostcert: libssh2 1.11.1 agrees the plain key and ignores @cert-authority, exit 60 on every pin; --hostpubsha256 still downloads (ADR-0068)
