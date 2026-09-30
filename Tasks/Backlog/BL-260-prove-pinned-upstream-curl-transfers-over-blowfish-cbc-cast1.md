---
id: BL-260
title: Prove pinned upstream curl transfers over blowfish-cbc, cast128-cbc and hmac-ripemd160
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-258]
touches: [Surl.Conformance.UnitTests]
requirement: FR-039
created: 2026-09-30
completed:
---
# BL-260 — Prove pinned upstream curl transfers over blowfish-cbc, cast128-cbc and hmac-ripemd160

## Goal

A pinned upstream curl 8.21.0 build that offers `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160`
and `hmac-ripemd160@openssh.com` (the Linux or macOS reference pin, libssh2 1.11.1 on OpenSSL)
completes an SFTP transfer against `surl --allow-weak-ssh-algorithms` with each of them agreed.

## Context

- BL-258 made `Surl.Protocol.Ssh.UnitLibrary` offer and run the four names behind
  `--allow-weak-ssh-algorithms` (ADR-0061), proven only against the SSH tests' own hand-written
  client (`SshTestPacketProtection`).
- The names are pinned as offered by upstream curl in
  `Surl.Conformance.UnitTests/UpstreamCurlOffersSshAlgorithmsTests.cs` (`OpenSslCipher`,
  `OpenSslMac`); the exchange tests live beside `UpstreamCurlLogsInToSurlOverSshTests.cs` and
  `PinnedUpstreamCurlOverSsh.cs`.
- Upstream curl chooses its cipher and MAC by libssh2's own preference, so each test must make
  surl's side the only overlap: this likely needs a way to narrow what surl offers, or curl's
  `--ssh-...` options if libssh2 honours an environment preference; decide and record it.
- Only a build pinned in `UpstreamCurlBuilds.json` is run (ADR-0003); a test for a pin absent on
  the machine is skipped the way the existing SSH conformance tests are. Never the Curl port.

## Acceptance criteria

- [ ] For each of `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` and `hmac-ripemd160@openssh.com`,
      a test in `Surl.Conformance.UnitTests` shows the pinned upstream curl agreeing that name with
      surl (from surl's log or curl's verbose output) and downloading a file over `sftp` with exit
      code 0 and the file's bytes on stdout.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-30: Created.
