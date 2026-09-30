---
id: BL-168
title: Offer ssh-ed25519 host keys and accept ssh-ed25519 user keys in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-162, BL-151]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-168 — Offer ssh-ed25519 host keys and accept ssh-ed25519 user keys in Surl.Protocol.Ssh

## Goal

`SshProtocolServer` serves an `ssh-ed25519` host key (signing the exchange hash with the
hand-built Ed25519) and verifies `ssh-ed25519` user-key signatures in `publickey`
authentication, and the host-key reader accepts Ed25519 keys in the formats BL-154's ADR
names.

## Context

- Decision: BL-154's ADR (host-key list and order, host-key file formats); BL-148's ADR (the
  library). Specification: RFC 8709 (the `ssh-ed25519` key and signature encodings), RFC 8032
  (Ed25519), OpenSSH `PROTOCOL.key` for the `openssh-key-v1` private-key format if the ADR
  reads it.
- Add the `ProjectReference` to `Surl.Cryptography.Ed25519.UnitLibrary`.
- Host-key signing is BL-160's path; user-key verification is BL-162's path; this task adds the
  Ed25519 algorithm to both.

## Acceptance criteria

- [ ] A fast test completes a key exchange with an `ssh-ed25519` host key and the test-side
      client verifies the signature with `Surl.Cryptography.Ed25519`.
- [ ] Fast tests accept a valid `ssh-ed25519` user-key signature for an authorized key, and
      refuse a flipped signature and a key blob of the wrong length.
- [ ] The host-key reader accepts an Ed25519 key in each format the ADR names, and refuses the
      malformed cases the ADR lists.
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
