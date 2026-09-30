---
id: BL-237
title: Answer connection-layer messages that arrive during a server-started SSH key re-exchange
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-163]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
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

- [ ] A fast test: the server passes its byte limit mid-transfer, the client sends
      `CHANNEL_DATA` before its `KEXINIT`, the re-exchange completes, and the data is
      delivered and answered under the new keys.
- [ ] Nothing but key exchange messages is written between the server's `KEXINIT` and its
      `NEWKEYS` (a fast test reads the server's packets in that window).
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
