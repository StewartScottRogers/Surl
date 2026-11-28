---
id: BL-1930
title: Correct the certificate-only AUTH TLS claims in the FTP server's comments
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1930 — Correct the certificate-only AUTH TLS claims in the FTP server's comments

## Goal

The FTP library's comments say `AUTH TLS` is available with a certificate or under TLS-SRP.

## Context

Found while working BL-1924 (2026-11-27), read from source. Comment text only; no behaviour change and no string literal
changes. Line numbers are as of that date and approximate. `Surl.Composition` (`ProtocolServerRegistrations`) passes
`ServerTlsComposition.IsTlsUpgradeAvailable` as `isTlsUpgradeAvailable`: a certificate (`--cert`, `--self-signed`) or
TLS-SRP (ADR-0122 decision 5). The library does not reference `Surl.Composition`, so name the type in `<c>`, not
`<see cref>`.

In `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary`:

- `FtpProtocolServer.cs`, remarks, about line 44: `AUTH` upgrades "only when the listener has a certificate".
- `FtpProtocolServer.cs`, the `isTlsUpgradeAvailable` param, about line 113.
- `FtpCommandResponder.cs`, the `isTlsUpgradeAvailable` param, about line 73.
- `FtpUpstreamTestCaseServer.cs`, remarks, about line 28: "`AUTH` is refused, as on a listener without a certificate".

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] The `FtpProtocolServer` remarks and both `isTlsUpgradeAvailable` params name both a certificate and TLS-SRP, and say `534` answers `AUTH` only when no upgrade is available.
- [x] The `FtpUpstreamTestCaseServer` remark compares its refusal of `AUTH` to a listener with no TLS upgrade available.
- [x] No comment in the library says `AUTH` depends on a certificate alone.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- 2026-11-27: Four XML doc comments edited in the session instead of through `align-and-document`: the task names each
  site, so delegating would only repeat the reading. `ServerTlsComposition.IsTlsUpgradeAvailable` (ADR-0122 decision 5)
  was read to confirm the claim.
- The comments say `534` answers `AUTH TLS` only when no upgrade is available. `AuthRefusal` answers `501`, `503` or
  `504` first for a missing argument, a connection already using TLS, or an unknown mechanism.
- Verified: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests 30366 passed,
  0 failed, 3 skipped. No changed line in the diff is anything but a comment.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. FTP library comments say AUTH TLS is available with a certificate or under TLS-SRP, 534 only when no upgrade is available
