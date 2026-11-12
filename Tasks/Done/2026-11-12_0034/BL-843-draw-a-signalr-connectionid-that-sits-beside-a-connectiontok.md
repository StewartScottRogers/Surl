---
id: BL-843
title: Draw a SignalR connectionId that sits beside a connectionToken from the seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-843 — Draw a SignalR connectionId that sits beside a connectionToken from the seedable identifier source

## Goal

A SignalR negotiate response that carries a `connectionToken` draws its `connectionId` from
`ExchangeContext.Identifiers`, while every value that admits a client to a connection stays on
`RandomNumberGenerator`.

## Context

- Decision: ADR-0124 decision 6 - a SignalR `connectionToken` is never seeded; generated identifiers that grant
  nothing are. Found by BL-786's audit.
- `Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary/SignalRHubConnections.cs:146` -
  `private static string NewId()` (16 bytes from `RandomNumberGenerator`, base64url) is used for both the
  `connectionId` and the `connectionToken` (lines 111 and 139).
- `SignalRNegotiatedConnection.UpgradeId` is `ConnectionToken ?? ConnectionId`: on a version 0 negotiate (no
  token) and on a direct WebSocket connection (`TakeUpNew`, line 111) the `connectionId` itself admits the
  upgrade, so there it is a token and must stay on `RandomNumberGenerator`.
- Fix: draw the `connectionId` from the exchange's `IIdentifierRandom` only when a `connectionToken` is also
  issued; the token, and the `connectionId` whenever it is the `UpgradeId`, stay cryptographic.

## Acceptance criteria

- [x] `SignalRHubConnectionsTests.Negotiate_Version1_SameSeededIdentifiers_GiveTheSameConnectionIdAndDifferentTokens`
      passes.
- [x] `SignalRHubConnectionsTests.Negotiate_Version0_ConnectionIdIsNotSeeded` passes: two version 0 negotiates with
      the same seeded source get different `connectionId`s.
- [x] The library stays at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

- 2026-11-11 (lane 6): Code written, not built - `global.json` pins .NET SDK 10.0.401, which is not installed (BL-1035). The change, left uncommitted for the shift to stash: `SignalRHubConnections.Negotiate` takes an `IIdentifierRandom identifiers` (null-guarded) and draws the `connectionId` from it only when `sendsConnectionToken`; the token, a version 0 `connectionId` and `TakeUpNew`'s ids stay on `RandomNumberGenerator`. `SignalRRequestAnswerer` passes `context.Identifiers`. Tests: new `UnitTestIdentifierRandom` (System.Random by seed), the two named tests plus `Negotiate_NullIdentifiers_Throws`; every existing `Negotiate` call passes a source, and `Negotiate_Twice_GivesFreshIds` uses seeds 1 and 2. Next run: build, run the fast tests, check coverage.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build and the fast tests cannot run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. SignalR connectionId beside a connectionToken now comes from the seedable identifier source; tokens and upgrade ids stay cryptographic
