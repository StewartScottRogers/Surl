---
id: BL-1080
title: Pass the exchange to SMB logins so their login events name smb or smbs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-871]
touches: [Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-12
completed:
---
# BL-1080 — Pass the exchange to SMB logins so their login events name smb or smbs

## Goal

The SMB server's `SmbNtlmV1Login` carries `LoginExchange.Of(context)`, so its `LoginJudgedEvent` names the exchange and `smb` or `smbs`.

## Context

BL-871 (ADR-0128 Amendment 1) added the optional `Exchange` parameter to `SmbNtlmV1Login`. Until the server passes it, logins are published as `smb` with no exchange ID.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Smb.UnitTests` shows the policy receiving the login with an `Exchange` whose ID and scheme are the connection's.
- [ ] The whole solution builds and the fast tests are green.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
