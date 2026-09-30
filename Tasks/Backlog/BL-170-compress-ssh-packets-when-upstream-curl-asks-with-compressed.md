---
id: BL-170
title: Compress SSH packets when upstream curl asks with --compressed-ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-162]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-170 — Compress SSH packets when upstream curl asks with --compressed-ssh

## Goal

When upstream curl runs with `--compressed-ssh`, `SshProtocolServer` negotiates the compression
algorithm BL-154's ADR measured curl offering (`zlib@openssh.com`, `zlib`, or both) and
compresses and decompresses packet payloads with the BCL's zlib, bounded so a small packet can
never inflate past `--max-message`.

## Context

- Decision: BL-154's ADR (the compression list measured with `--compressed-ssh`, and its order;
  `none` stays offered). Specification: RFC 4253 section 6.2 (`zlib`, starting at `NEWKEYS`);
  OpenSSH's `PROTOCOL` for `zlib@openssh.com` (delayed until `USERAUTH_SUCCESS`); one
  compression context per direction for the connection's life, each packet flushed with a
  partial (sync) flush.
- BCL: `System.IO.Compression.ZLibStream` / `DeflateStream` over the stream per direction; no
  package.
- Bound (ADR-0006, "every store a peer can fill is bounded"): decompressed payload counted
  against `ExchangeLimits.MaxMessageBytes`, stopping the moment it is passed and ending the
  connection with the ADR's `DISCONNECT`.

## Acceptance criteria

- [ ] A fast test negotiates each compression algorithm the ADR lists with the test-side client
      and round-trips packets; `zlib@openssh.com` stays uncompressed until `USERAUTH_SUCCESS`.
- [ ] A fast test sends a packet that inflates past `MaxMessageBytes` and the connection ends
      with the ADR's `DISCONNECT` without inflating further.
- [ ] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
