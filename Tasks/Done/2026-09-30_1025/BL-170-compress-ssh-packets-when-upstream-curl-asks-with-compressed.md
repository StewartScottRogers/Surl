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
completed: 2026-09-30
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

- [x] A fast test negotiates each compression algorithm the ADR lists with the test-side client
      and round-trips packets; `zlib@openssh.com` stays uncompressed until `USERAUTH_SUCCESS`.
- [x] A fast test sends a packet that inflates past `MaxMessageBytes` and the connection ends
      with the ADR's `DISCONNECT` without inflating further.
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- No new ADR: ADR-0051 decisions 2 and 9 already fix the offer (`none`, `zlib@openssh.com`,
  `zlib`), the start points and `DISCONNECT` 6 `Compression error`. Upstream curl's
  `--compressed-ssh` list (`zlib`, `zlib@openssh.com`, `none`) was measured there (run F), so
  against the pinned build the negotiation is `zlib`.
- Built as `SshZlibCompressor` / `SshZlibDecompressor` over the BCL's `ZLibStream` (RFC 1950,
  as libssh2's `deflateInit`), one per direction, owned by `SshPacketWriter` / `SshPacketReader`.
  Each payload is sync-flushed (`ZLibStream.Flush`, ending `00 00 FF FF`), which zlib's inflate
  accepts the same as libssh2's own partial flush.
- Choice: a **new zlib stream after every `NEWKEYS`**, as RFC 4253 section 6.2 says and libssh2
  does (it re-initialises its compression at each key exchange); OpenSSH keeps one stream for
  the connection, but upstream curl is the oracle. `zlib@openssh.com` starts both ways right
  after the server writes `USERAUTH_SUCCESS` (itself uncompressed), and after a later re-exchange
  starts at `NEWKEYS`, since the login has succeeded (libssh2's `use_in_auth` flag reads the same).
- The bound: a payload inflates in chunks of at most `limit - inflated + 1` bytes, so at most one
  byte past `MaxMessageBytes` (or `Array.MaxLength` when the limit is 0) is ever produced before
  `DISCONNECT` 6. A payload that does not inflate, or inflates to nothing, is also `DISCONNECT` 6.
- The transport is now `IDisposable` so the zlib streams' native state is freed when the
  connection ends.
- Tests: `SshCompressionTests` (8 cases); the test client compresses and inflates with its own
  `ZLibStream`s. SSH tests 761, all green; coverage 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl compresses SSH packets with zlib and zlib@openssh.com when upstream curl asks with --compressed-ssh, bounded by --max-message
