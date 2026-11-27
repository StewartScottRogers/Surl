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
completed:
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

- [ ] The `FtpProtocolServer` remarks and both `isTlsUpgradeAvailable` params name both a certificate and TLS-SRP, and say `534` answers `AUTH` only when no upgrade is available.
- [ ] The `FtpUpstreamTestCaseServer` remark compares its refusal of `AUTH` to a listener with no TLS upgrade available.
- [ ] No comment in the library says `AUTH` depends on a certificate alone.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
