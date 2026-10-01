---
id: BL-261
title: Prove pinned upstream curl transfers over blowfish-cbc, cast128-cbc and hmac-ripemd160
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-258, BL-250, BL-262, BL-263]
touches: [Surl.Conformance.UnitTests]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-261 — Prove pinned upstream curl transfers over blowfish-cbc, cast128-cbc and hmac-ripemd160

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

- [x] For each of `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` and `hmac-ripemd160@openssh.com`,
      a test in `Surl.Conformance.UnitTests` shows the pinned upstream curl agreeing that name with
      surl (from surl's log or curl's verbose output) and downloading a file over `sftp` with exit
      code 0 and the file's bytes on stdout.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-09-30 (lane 1): cannot be finished yet; three prerequisites, now in `depends-on`:
  - BL-250: surl still refuses `--allow-weak-ssh-algorithms` (`CommandLineRunner.UnavailableOptions`)
    and `ComposeProtocolServers` never passes it to `SshAlgorithmOffer.Default`, so no surl process
    offers the four names yet.
  - BL-262 (filed): libssh2 picks the first name in its own list that surl offers, and curl has no
    option to change it; rewriting a KEXINIT on the wire breaks the exchange hash. Decided to give
    surl sshd-style `--ssh-ciphers`/`--ssh-macs` rather than have the conformance test compose
    `SshProtocolServer` itself, so the test runs the real `surl` command line the goal names.
  - BL-263 (filed): the only pins offering the names are the Linux and macOS reference builds, not
    installed here; the pinned OpenSSL static-curl Windows build (same sources) should be admitted
    for these SSH measurements by ADR. It also has to measure whether OpenSSL 4 lets libssh2 run
    blowfish-cbc and cast128-cbc at all (legacy provider), which may change what this task proves.

- 2026-09-30 (lane 4): `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests` (Integration) starts `surl -v --throwaway-hostkey --user ... --allow-weak-ssh-algorithms` with `--ssh-ciphers <name>` (cipher cases) or `--ssh-ciphers aes128-ctr --ssh-macs <name>` (MAC cases), and asserts exit 0, the file bytes on stdout, and `cipher <name>/<name>` or `MAC <name>/<name>` in surl's `SSH negotiated` note. All four passed on Windows against the static-curl OpenSSL pin.
- Default taken: the MAC cases narrow the cipher to `aes128-ctr`, because libssh2 would otherwise agree an AEAD cipher (chacha20-poly1305) and the MAC would go unused (`MAC implicit`).
- Default taken: the build is chosen by `OperatingSystem.IsWindows()` - the supplementary OpenSSL pin on Windows (ADR-0063 decision 1), the reference pin elsewhere; the cipher cases write an `openssl.cnf` activating `default` and `legacy` into the isolated curl home and set `OPENSSL_CONF` to it (ADR-0063 decision 3). No new ADR: ADR-0063 and ADR-0066 already decide both.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Backlog. Waits on BL-250 (surl serves --allow-weak-ssh-algorithms), BL-262 (narrow the SSH offer) and BL-263 (admit an OpenSSL build on Windows)
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned upstream curl on OpenSSL agrees blowfish-cbc, cast128-cbc, hmac-ripemd160 and hmac-ripemd160@openssh.com with surl and downloads over sftp
