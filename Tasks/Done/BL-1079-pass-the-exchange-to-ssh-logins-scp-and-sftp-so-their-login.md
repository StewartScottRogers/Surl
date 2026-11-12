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
completed: 2026-11-12
---
# BL-1079 — Pass the exchange to SSH logins (scp and sftp) so their login events name sftp or scp

## Goal

Every SSH password and public-key login the SSH server judges carries `LoginExchange.Of(context)`, so its `LoginJudgedEvent` names the exchange and `sftp` or `scp`.

## Context

BL-871 (ADR-0128 Amendment 1) added the optional `Exchange` parameter to `SshPasswordLogin` and `SshPublicKeyLogin`. Until the server passes it, logins are published as `ssh` with no exchange ID.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Ssh.UnitTests` shows the policy receiving each login request with an `Exchange` whose ID and scheme are the connection's.
- [x] The whole solution builds and the fast tests are green.

## Notes

SshUserAuthentication takes a LoginExchange (from LoginExchange.Of(context) in SshProtocolServer) and passes it on every password, keyboard-interactive and public-key login. Test: SshUserAuthenticationTests.AnswerLoginRequest_PasswordAndPublicKeyQuery_CarryTheConnectionsExchange.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. SSH password and public-key logins carry the connection's exchange, so login events name sftp or scp
