---
id: BL-844
title: Draw AMQP 0-9-1 generated names and AMQP 1.0 container IDs and dynamic queue names from the seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-844 — Draw AMQP 0-9-1 generated names and AMQP 1.0 container IDs and dynamic queue names from the seedable identifier source

## Goal

AMQP 0-9-1 server-generated queue and consumer names, AMQP 1.0 dynamic queue names and the AMQP 1.0
`container-id` come from the seedable identifier source, and no AMQP 1.0 identifier is drawn once per process.

## Context

- Decision: ADR-0124 decision 6 names AMQP generated queue names and AMQP 1.0 container IDs as seedable, and
  forbids static mutable state. Found by BL-786's audit.
- `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0BrokerConnection.Exchanges.cs:58` -
  `GenerateName(prefix)` from `RandomNumberGenerator.GetBytes(16)`.
- `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1BusNodeDirectory.cs:53` - dynamic queue name
  from `RandomNumberGenerator.GetBytes(16)`.
- `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerOptions.cs:15,21,43` - `ContainerId`
  defaults to `NewContainerId()`, and the static `Default` instance therefore holds one container ID drawn at type
  initialisation and shared by every container in the process that takes `Default`.
- Fix: per-connection names from `context.Identifiers`; the container ID drawn when the container (server) is
  created from an injected `IIdentifierRandom` (defaulting to `CryptographicIdentifierRandom`), not at type
  initialisation - `Default` either loses the drawn ID or goes. AMQP 1.0 is not composed yet, so
  `Surl.Composition` is not touched; whichever task composes it passes the instance's server-life source.

## Acceptance criteria

- [x] `Amqp0BrokerConnectionTests.QueueDeclare_EmptyName_SameSeededIdentifiers_GeneratesTheSameName` passes.
- [x] `Amqp1BusNodeDirectoryTests.DynamicQueue_SameSeededIdentifiers_GetsTheSameName` passes.
- [x] `Amqp1ContainerOptionsTests.TwoContainers_UnseededDefaults_SendDifferentContainerIds` and
      `..._SameSeededSource_SendTheSameContainerId` pass; no static member of `Amqp1ContainerOptions` holds a drawn
      value.
- [ ] Both libraries stay at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

- Implemented on 2026-11-11 (lane 3) and left uncommitted in the lane's worktree for the shift to stash,
  because no build can run under the committed `global.json`: the pinned SDK 10.0.401 is not installed
  (BL-1035). With `global.json` temporarily pointed at 11.0.100 (then put back), `dotnet build -warnaserror`
  of both `.UnitTests` projects was clean, the fast tests passed (Amqp0 328, Amqp1 708), and coverage was
  Amqp1 100% line / 100% branch, Amqp0 100% line / 99.6% branch. The Amqp0 branch gap is compiler-generated
  `await` branches in `Amqp0ExchangeFiles.ReadAsync`/`WriteAsync`, which this task does not touch.
  Once BL-1035 is Done: restore the stash (or redo the change below), build, test, tick the last box.
- The change:
  - Amqp0: `GenerateName` became an instance method drawing 16 bytes from `context.Identifiers`, so both
    `amq.gen-` queue names and `amq.ctag-` consumer tags are seedable.
  - Amqp1: `Amqp1BusNodeDirectory` takes an `IIdentifierRandom`; `Amqp1ContainerConnection` builds its
    default directory with `context.Identifiers` (its node-directory factory is now nullable for that).
  - `Amqp1ContainerOptions.ContainerId` is `string?` with no initialiser, so `Default` holds nothing drawn;
    `NewContainerId(IIdentifierRandom)` replaces the parameterless one. `Amqp1Container` takes an optional
    `IIdentifierRandom identifiers` and draws the id once in its constructor when the options give none,
    exposing it as `Amqp1Container.ContainerId`. `Amqp1ContainerConnection` throws `ArgumentException`
    for options without an id, because only the container draws it.
- Choice: the unseeded default is a new internal `Amqp1UnseededIdentifierRandom` over
  `RandomNumberGenerator`, not `Surl.Core`'s `CryptographicIdentifierRandom`. No protocol library references
  `Surl.Core`, and the Abstractions default is internal, so referencing `Core` only for this would add a
  dependency edge the ADR-0002 table does not list. The bytes are the same. The test projects reference
  `Surl.Core` (as `Surl.Protocol.Ftp.UnitTests` does) to use the real `SeededIdentifierRandom`.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so no build under global.json can run; the change is done and passes under SDK 11 (left uncommitted for the shift stash).
