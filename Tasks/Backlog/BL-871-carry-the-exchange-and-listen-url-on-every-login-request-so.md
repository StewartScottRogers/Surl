---
id: BL-871
title: Carry the exchange and listen URL on every login request so login events and refuse-login rules see them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-791]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Protocols]
requirement: none
created: 2026-11-05
completed:
---
# BL-871 — Carry the exchange and listen URL on every login request so login events and refuse-login rules see them

## Goal

Every `LoginJudgedEvent` names its exchange and the scheme it was really made over (`sftp`, not `ssh`;
`https`, not `http`), and a `refuse-login` rule scoped by `listener=` matches a login, because each login
request in `Surl.Protocol.Abstractions` carries its exchange ID and listen URL.

## Context

- ADR-0128 (BL-791) publishes logins with `ExchangeId` null and names HTTP, SSH and SMB logins by family
  (`http`, `ssh`, `smb`), because `StartHttpConnection`/`HttpAuthenticationRequest`, `SshPasswordLogin`,
  `SshPublicKeyLogin` and `SmbNtlmV1Login` carry no scheme and no login request carries an exchange ID or
  listen URL. `FaultPoint.ListenUrl` is therefore never set at `FaultStage.Login`.
- ADR-0124 decision 4: a refuse-login rule scopes by account and listener or scheme; BL-807 queries events
  by exchange.
- Change: add the exchange ID and listen URL (optional, defaulting to unknown, so every existing caller
  compiles) to the login requests in `Foundation/Surl.Protocol.Abstractions.UnitLibrary`; pass them from
  each server; `AuthenticationPolicy` sets `LoginJudgedEvent.ExchangeId`, the real scheme, and
  `FaultPoint.ExchangeId`, `ListenUrl` and `Scheme`.
- It touches a shared contract and every server that judges a login, so `task-planner` should split it per
  server family (contract first) before it starts; `touches` names `Protocols` whole until then.

## Acceptance criteria

- [ ] A named test in `Surl.Authentication.UnitTests` shows an SSH login over `sftp` published with scheme
      `sftp` and its exchange ID, and a `refuse-login` rule with a `Listener` refusing logins on that listener only.
- [ ] Every server that judges a login passes its exchange ID and listen URL (one named test per server family).
- [ ] ADR-0128 is amended to say the real scheme and exchange are now published.
- [ ] The whole solution builds and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
