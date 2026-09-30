---
id: BL-237
title: Answer connection-layer messages that arrive during a server-started SSH key re-exchange
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-163]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-30
---
# BL-237 — Answer connection-layer messages that arrive during a server-started SSH key re-exchange

## Goal

When the server starts a key re-exchange (1 GiB or one hour, ADR-0051 decision 2.1), channel
messages the client sent before it saw the server's `KEXINIT` are handled rather than refused.

## Context

- RFC 4253 section 9 lets the client keep sending until it has seen the server's `KEXINIT`;
  only after sending its own `KEXINIT` may it send nothing but key exchange messages. The
  server itself must send nothing else until its `NEWKEYS`.
- BL-161 built the re-exchange in `SshTransportHandshake.ReExchangeAsync`. With no channels
  yet, it treats any message other than `IGNORE`, `DEBUG` or `UNIMPLEMENTED` before the
  client's `KEXINIT` as `DISCONNECT` 2. Once BL-163's channels exist, a long SCP or SFTP
  transfer passing 1 GiB would be cut off by that.
- Messages received in that window are to be handed to the connection layer; anything they
  would make the server send waits until after the server's `NEWKEYS`.

## Acceptance criteria

- [x] A fast test: the server passes its byte limit mid-transfer, the client sends
      `CHANNEL_DATA` before its `KEXINIT`, the re-exchange completes, and the data is
      delivered and answered under the new keys.
- [x] Nothing but key exchange messages is written between the server's `KEXINIT` and its
      `NEWKEYS` (a fast test reads the server's packets in that window).
- [x] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Decided and recorded in ADR-0060 (amends ADR-0051 decision 2.2, choice 5): in a
  server-started re-exchange, messages before the client's `KEXINIT` other than
  `IGNORE`/`DEBUG`/`UNIMPLEMENTED` and key exchange messages are held by
  `SshTransportHandshake` and returned first by `ReadMessageAsync` after `NEWKEYS`, so the
  existing message loop answers them in order and nothing is written inside the exchange. The
  write gate the re-exchange already holds keeps handler writes out. Chosen over answering them
  during the exchange with a deferred write queue: one queue of inbound messages, no second
  writer path, no deadlock on the gate.
- Held messages keep their own packet sequence number (`LastMessageSequenceNumber`), so an
  `UNIMPLEMENTED` answering one names the right packet even after the strict-kex reset.
- Held bytes are bounded by `SshReExchangeLimits.HeldBytes` (default 10 channel windows of
  2 MiB plus 1 MiB), past it `DISCONNECT` 2, so a client that never sends `KEXINIT` cannot make
  the server hold without end.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0060 and the ADR-0051 pointer; no
  task in Doing names it (BL-171 touches only the Cli and Console projects).
- Tests: `SshReExchangeTests` gains the mid-transfer channel data test (both criteria), held
  unknown messages answered `UNIMPLEMENTED` with their sequence number, key exchange messages
  in the window `DISCONNECT` 2, and the held-bytes limit; the old `DISCONNECT` 2 test for message
  200 is replaced. 965 SSH tests pass; the new ones passed 8 repeated runs.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Messages sent before the client's KEXINIT in a server-started SSH re-exchange are held and answered after NEWKEYS (ADR-0060)
