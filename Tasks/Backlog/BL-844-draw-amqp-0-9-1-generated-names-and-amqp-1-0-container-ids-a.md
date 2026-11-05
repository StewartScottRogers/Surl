---
id: BL-844
title: Draw AMQP 0-9-1 generated names and AMQP 1.0 container IDs and dynamic queue names from the seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-839]
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

- [ ] `Amqp0BrokerConnectionTests.QueueDeclare_EmptyName_SameSeededIdentifiers_GeneratesTheSameName` passes.
- [ ] `Amqp1BusNodeDirectoryTests.DynamicQueue_SameSeededIdentifiers_GetsTheSameName` passes.
- [ ] `Amqp1ContainerOptionsTests.TwoContainers_UnseededDefaults_SendDifferentContainerIds` and
      `..._SameSeededSource_SendTheSameContainerId` pass; no static member of `Amqp1ContainerOptions` holds a drawn
      value.
- [ ] Both libraries stay at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
