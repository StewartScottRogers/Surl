---
id: BL-159
title: Exchange SSH identification strings, binary packets and KEXINIT in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-154]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-29
---
# BL-159 — Exchange SSH identification strings, binary packets and KEXINIT in Surl.Protocol.Ssh

## Goal

`Surl.Protocol.Ssh` has an `SshProtocolServer` (`IConnectionProtocolServer`, schemes `scp` and
`sftp`) that exchanges identification strings, reads and writes unencrypted SSH binary
packets, sends its `SSH_MSG_KEXINIT` and negotiates algorithms against upstream curl's, as
BL-154's ADR decides - the foundation every later SSH task builds on.

## Context

- Decision: BL-154's ADR (identification string, algorithm lists and order, limits,
  `DISCONNECT` codes, verbose notes). Specification: RFC 4253 sections 4.2 (identification),
  6 (binary packet protocol: `packet_length`, `padding_length`, padding of 4 to 255 bytes, the
  8-byte block multiple before a cipher is agreed), 7.1 (KEXINIT and the negotiation rule:
  the first client algorithm the server also supports), 11 (`IGNORE`, `DEBUG`,
  `UNIMPLEMENTED`, `DISCONNECT`).
- Seam: ADR-0004 - the server gets an `IConnection` and an `ExchangeContext` and never a socket;
  tests replay bytes through `InMemoryConnection`. Randomness (the KEXINIT cookie, padding)
  comes from an injected source, so tests are deterministic.
- Fixtures: BL-154's ADR records curl's identification line and KEXINIT as measured; put them
  under `Surl.Protocol.Ssh.UnitTests/Fixtures/<case>/` (embedded, with a `README.md` naming the
  build, SHA-256, command line and date, as BL-017 and BL-036 did).
- Limits (ADR-0006): a packet longer than `ExchangeLimits.MaxMessageBytes` is refused before its
  body is read; the identification and key exchange run under the head timeout; each answered
  with the ADR's `DISCONNECT` reason then a graceful close.
- The key exchange itself is BL-160; after negotiation this task may end the connection with
  the ADR's `DISCONNECT` for "key exchange not implemented", and BL-160 replaces that.
- Code to copy (never expectations): the Curl port's
  `Curl.Protocol.Ssh.UnitLibrary/Transport/` (`SshIdentificationExchange`, `SshPacketReader`,
  `SshPacketWriter`, `SshWireReader`, `SshWireWriter`) and `Negotiation/` (`SshKexInit`,
  `SshAlgorithmNegotiator`), turned to the server's side.

## Acceptance criteria

- [x] A fast test replays each recorded curl identification + KEXINIT fixture and asserts surl's
      identification line, its KEXINIT bytes (fixed cookie) with the ADR's lists in order, and
      the negotiated algorithms.
- [x] Fast tests cover: an identification line over 255 bytes or without `SSH-2.0-`; a packet
      over `MaxMessageBytes`; padding shorter than 4 bytes or a length not a block multiple; no
      algorithm in common (the ADR's `DISCONNECT` reason); the head timeout (fake
      `TimeProvider`); a connection closed mid-packet; an `IGNORE` and a `DEBUG` message skipped.
- [x] `Surl.Protocol.Ssh.UnitLibrary` references only what ADR-0002 and BL-148's ADR allow;
      `ProtocolIsolationTests` pass.
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no `Integration` test in `Surl.Protocol.Ssh.UnitTests` and no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Fixtures are ADR-0051's runs D (`sftp-insecure`) and F (`sftp-insecure-compressed`); D, E,
  G, H and I differ only in cookie and padding, so one uncompressed case stands for them.
  Against them the server agrees `diffie-hellman-group-exchange-sha256`, `rsa-sha2-512`,
  `chacha20-poly1305@openssh.com` (MAC implicit), `none` or `zlib`, strict kex on.
- `SshTransportHandshake.RunAsync` returns the agreed algorithms once it has read the key
  exchange method's first message; `SshProtocolServer` answers that with `DISCONNECT` 11 "Key
  exchange not implemented". Keeping the refusal in the server leaves BL-160 one place to
  replace, and gives the handshake no unreachable success path (100% line coverage).
- The expected server `KEXINIT` in `SshProtocolServerTests` is written out from ADR-0051
  decision 2's table, not taken from the server's own writer, so the test checks the lists and
  their order independently. Test framing helpers (`SshTestExchange`) build packets by hand.
- Measured: `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` 100% line, 100%
  branch, 0 failing members, worst CRAP 10; `ProtocolIsolationTests` pass; 85 SSH tests, none
  `Integration`, all over `InMemoryConnection`.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH identification, binary packets, KEXINIT and negotiation work against curl's recorded opening; 100% coverage
