---
id: BL-161
title: Protect SSH packets with AES-CTR, AES-GCM and HMAC-SHA2 and rekey in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-160]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-29
---
# BL-161 — Protect SSH packets with AES-CTR, AES-GCM and HMAC-SHA2 and rekey in Surl.Protocol.Ssh

## Goal

After `NEWKEYS`, `SshProtocolServer` encrypts and authenticates every packet with the
BCL-built ciphers and MACs BL-154's ADR lists (AES-CTR, AES-GCM, HMAC-SHA2 and their
encrypt-then-MAC forms), and re-keys when the client or the ADR's limits ask.

## Context

- Decision: BL-154's ADR (cipher and MAC lists, order, re-key limits, `DISCONNECT` codes).
- Specifications: RFC 4344 (`aes128-ctr`, `aes192-ctr`, `aes256-ctr`, composed from
  `Aes.EncryptEcb`, BL-148's ADR); RFC 5647 as OpenSSH's `PROTOCOL` modifies it
  (`aes128-gcm@openssh.com`, `aes256-gcm@openssh.com`, with `AesGcm`; offered only where
  `AesGcm.IsSupported`, as the ADR decides); RFC 6668 (`hmac-sha2-256`, `hmac-sha2-512`) and
  OpenSSH's `-etm@openssh.com` forms; RFC 4253 section 6.4 (MAC over the sequence number) and
  section 9 (re-exchange). A received MAC or tag is compared with
  `CryptographicOperations.FixedTimeEquals`; a failure ends the connection with the ADR's
  `DISCONNECT`.
- The sequence number wraps at 2^32 (RFC 4253 section 6.4); strict kex resets it if BL-154's
  ADR decided strict kex.
- Code to copy (never expectations): the Curl port's `PacketProtection/`
  (`AesCtrSshCipher`, `AesGcmPacketProtection`, `CipherAndMacPacketProtection`, `SshMac`).

## Acceptance criteria

- [x] AES-CTR passes NIST SP 800-38A section F.5's CTR vectors (F.5.1, F.5.3, F.5.5), cited.
- [x] For each cipher and MAC combination this task covers, a fast test round-trips packets with
      the test-side client of BL-160, and a flipped ciphertext or MAC byte ends the connection
      with the ADR's `DISCONNECT`.
- [x] A fast test re-keys mid-session (client `KEXINIT` after `NEWKEYS`) and the session goes on
      under the new keys.
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

**What was built (2026-09-29).**
- `SshPacketProtection` (abstract, with `None`), `SshCipherAndMacProtection` (AES-CTR with an
  HMAC, encrypt-and-MAC or `-etm`), `SshAesGcmProtection`, `SshAesCtr` and `SshHmac`.
  `SshPacketReader` and `SshPacketWriter` seal and open packets with the protection in force.
  `SshTransportHandshake` switches both directions after `NEWKEYS` and runs re-exchanges
  (`ReExchangeAsync`), started by the client's `KEXINIT` or by the server at
  `SshReExchangeLimits` (1 GiB or one hour). `SshProtocolServer` then reads the client's
  messages in a loop.
- Tests: `SshAesCtrTests` (SP 800-38A F.5.1, F.5.3 and F.5.5, plus the counter wrapping at
  2^128), `SshPacketProtectionTests` (all 14 cipher and MAC combinations round-tripped, with a
  flipped ciphertext byte and a flipped MAC byte for each answered `DISCONNECT` 5), and
  `SshReExchangeTests` (re-exchange started by the client, and by the server at the read-byte,
  write-byte and time limits). The test-side client is BL-160's `SshTestKeyExchangeClient`,
  extended with a cipher and MAC choice, plus `SshTestPacketProtection` (its own AES-CTR, GCM
  and HMAC framing, written from the RFCs so the server is never checked against its own code)
  and `SshTestPipeConnection` (a `Channel`-based in-memory duplex connection, no socket).
- Measured: `Surl.Protocol.Ssh.UnitLibrary` at 100% line, 100% branch, 234 members, worst CRAP
  10, 0 failing. The full solution run of `Measure-CodeQuality.ps1` did not finish in 26 minutes
  on this lane, so the SSH test project was run with `--collect:"Code Coverage;Format=cobertura"`
  and the script was pointed at that report with `-SkipTestRun -ResultsDirectory`.

**Decisions (Claude under Stewart's delegation).** The ADR edit is filed as BL-236, because
BL-155 had `Documentation/Planning/Decisions` in `Doing` at the same time.
- After `NEWKEYS`, a message the server does not know is answered `UNIMPLEMENTED` with its
  sequence number (RFC 4253 section 11.4). That includes a key exchange method message or
  `NEWKEYS` outside a key exchange, as OpenSSH answers them.
- `SERVICE_REQUEST` is `DISCONNECT` 11, `User authentication not implemented`, until BL-162
  lands. This follows the pattern of the ADR's other `NEWKEYS` placeholders. `DISCONNECT` 7
  was not used: the ADR keeps 7 for a service other than `ssh-userauth`.
- The re-exchange limits are checked between the client's packets, so an idle connection is
  re-keyed when it next sends. The idle and head timeouts bound it in the meantime.
- Strict kex's ordering rule (no `IGNORE` during the exchange) applies to the first exchange
  only; the sequence numbers are reset after every `NEWKEYS`, as ADR-0051 decision 2.1 says.
- When the server starts a re-exchange, a message before the client's `KEXINIT` other than
  `IGNORE`, `DEBUG` or `UNIMPLEMENTED` is `DISCONNECT` 2. No such message exists before
  channels do. BL-237 (after BL-163) changes this so the channel layer gets them.
- Where one direction's cipher is not built yet (`chacha20-poly1305@openssh.com`, BL-169),
  `DISCONNECT` 11 is still sent unprotected after `NEWKEYS`, as BL-160's placeholder was.
- Protections are not `IDisposable`. The `Aes` and `AesGcm` objects of replaced keys are left
  to the finalizer: at most one set per re-exchange.
- `ManualTimeProvider` (tests) now overrides `GetTimestamp` so elapsed time follows `Advance`.

**Follow-ups filed:** BL-236 (record the decisions above in ADR-0051) and BL-237 (connection
messages during a server-started re-exchange).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH packets are protected with AES-CTR, AES-GCM and HMAC-SHA2 (and -etm) after NEWKEYS, and re-keyed by client KEXINIT or the server's 1 GiB / 1 h limits
