---
id: BL-263
title: Admit the pinned OpenSSL static-curl Windows build for SSH weak-algorithm measurements
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [UpstreamCurlBuilds.json, Documentation/Planning/Decisions, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-263 — Admit the pinned OpenSSL static-curl Windows build for SSH weak-algorithm measurements

## Goal

The pinned supplementary build `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`
(upstream curl 8.21.0, libssh2 1.11.1 on OpenSSL 4.0.1, already installed and pinned for SMB and
Negotiate, ADR-0030, ADR-0042) is also admitted for SSH measurements the Windows reference build
cannot make: the OpenSSL-only algorithms `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` and
`hmac-ripemd160@openssh.com` (ADR-0061).

## Context

- The Windows reference build (Git for Windows, WinCNG) does not offer those four names
  (`UpstreamCurlOffersSshAlgorithmsTests.WinCngCipher`, `WinCngMac`); only the Linux and macOS
  reference pins do, and neither is installed on Stewart's Windows machine, so BL-261's tests
  would never run there.
- The static-curl Windows build is the same source, libssh2 and OpenSSL release as the Linux and
  macOS pins (ADR-0016), so it is the closest like-for-like Windows measurement. No download is
  needed: it is pinned by SHA-256 already.
- Extending a supplementary build's role is a decision (ADR-0017): record it in an ADR "Decided by
  Claude under Stewart's delegation", widen the build's `origin` text in `UpstreamCurlBuilds.json`,
  and keep `UpstreamCurlBuildPinsTests` green.
- Measure first: record the build's SSH KEXINIT with `ClientKexInitRecorder` (as
  `UpstreamCurlOffersSshAlgorithmsTests` does) and pin it; check whether OpenSSL 4's default
  provider lets libssh2 actually run `blowfish-cbc` and `cast128-cbc` (they live in OpenSSL 3+'s
  legacy provider) and record the answer in the ADR - it decides what BL-261 can prove.

## Acceptance criteria

- [x] An ADR admits the build for these SSH measurements and states why.
- [x] `UpstreamCurlBuilds.json` names the new use in the build's `origin`; `UpstreamCurlBuildPinsTests` pass.
- [x] An Integration test in `Surl.Conformance.UnitTests` pins that build's `sftp://` KEXINIT name lists.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-09-30 (lane 1): delivered in-session rather than through `align-and-document`, because the
  task needed a measurement and an Integration test as well as the ADR.
- Measured: the build's sftp KEXINIT equals the Linux and macOS pins' OpenSSL lists exactly;
  pinned by `KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms`, which uses the new
  `PinnedUpstreamCurl.RunSupplementaryBuildWithEnvironmentAsync`.
- Measured with a throwaway, uncommitted narrowing of surl's offer: `hmac-ripemd160` and
  `hmac-ripemd160@openssh.com` download fine; `blowfish-cbc` and `cast128-cbc` are agreed and then
  curl crashes (exit -1073741819, 0xC0000005) - OpenSSL 4 keeps both in the `legacy` provider,
  which libssh2 never loads. With `OPENSSL_CONF` naming a file that activates `default` and
  `legacy`, both download fine. ADR-0063 decision 3: BL-261 sets `OPENSSL_CONF` for its two
  cipher cases.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The static-curl OpenSSL Windows pin is admitted for the four OpenSSL-only SSH algorithms (ADR-0063); its sftp KEXINIT is pinned, and Blowfish/CAST-128 need OPENSSL_CONF's legacy provider
