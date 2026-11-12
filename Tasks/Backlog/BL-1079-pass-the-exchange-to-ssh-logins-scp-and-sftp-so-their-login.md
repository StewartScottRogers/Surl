---
id: BL-1079
title: Pass the exchange to SSH logins (scp and sftp) so their login events name sftp or scp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-871]
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-11-12
completed:
---
# BL-1079 — Pass the exchange to SSH logins (scp and sftp) so their login events name sftp or scp

## Goal

Every SSH password and public-key login the SSH server judges carries `LoginExchange.Of(context)`, so its `LoginJudgedEvent` names the exchange and `sftp` or `scp`.

## Context

BL-871 (ADR-0128 Amendment 1) added the optional `Exchange` parameter to `SshPasswordLogin` and `SshPublicKeyLogin`. Until the server passes it, logins are published as `ssh` with no exchange ID.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Ssh.UnitTests` shows the policy receiving each login request with an `Exchange` whose ID and scheme are the connection's.
- [ ] The whole solution builds and the fast tests are green.

## Notes

## Log

- 2026-11-12: Created.
