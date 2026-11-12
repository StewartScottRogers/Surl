---
id: BL-871
title: Carry the exchange and listen URL on every login request so login events and refuse-login rules see them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-791, BL-1035]
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

- 2026-11-11 (lane 3): first pass written, then stopped before any build. The pinned SDK 10.0.401 in
  `global.json` is not installed on the dark factory machine (`dotnet build` cannot find it), as for
  BL-923 and BL-960, so the task waits on BL-1035. The unfinished code is left in the shift's stash:
  - Contract: new `LoginExchange(long ExchangeId, ListenUrl ListenUrl)` in Abstractions, with `Scheme`,
    `ReportedListenUrl` and `Of(ExchangeContext)`. Optional `LoginExchange? Exchange = null` is last on
    `PasswordLogin`, `ApopLogin`, `SshPasswordLogin`, `SshPublicKeyLogin`, `SmbNtlmV1Login` and
    `SaslExchangeStart`. A new default member, `IAuthenticationPolicy.StartHttpConnection(tlsSession, exchange)`,
    ignores the exchange. Exchange IDs are per connection, so HTTP carries the exchange on the session,
    not on each `HttpAuthenticationRequest`. `SshNoneLogin` is never published, so it gets nothing.
  - Policy: `ConcludeCheckedLoginAsync`, `IsRefusedByFaultAsync` and `PublishJudgedLogin` take the
    exchange. Its scheme wins over the request's or family's scheme. The `FaultPoint` gets `ExchangeId`,
    `ListenUrl` (the reported form) and `Scheme`, and `FaultAppliedEvent` gets the exchange ID.
    `HttpAuthenticationSession` and `SaslExchangeContext` carry it.
  - Tests: `LoginExchangeTests` (Abstractions) and six new tests in `LoginJudgementPolicyTests`.
    `SpendingFaultPlan` now also matches `Listener`.
- Scope decision (default taken, per this task's Context): BL-871 is the contract-first part, so it
  covers the contract, the policy and the ADR-0128 amendment. Each server family's wiring
  (`LoginExchange.Of(context)` on its login requests) goes to `task-planner` as one follow-up task per
  family (HTTP family, SSH, SMB, mail, LDAP, MQTT, the other password-login servers). Each depends on
  BL-871. Acceptance criterion 2 moves to those tasks, and `touches` here can then drop `Protocols`.
  Still to do once it builds: build, the fast tests, amend ADR-0128, file those tasks and narrow the criteria.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so nothing builds; the first pass of the contract and policy is in the shift's stash
- 2026-11-12: Backlog -> Doing.
