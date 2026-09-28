# Surl.Core.UnitLibrary

Phase 1.

The serving engine: starts a listener for each listen URL, hands each accepted connection
or datagram flow to the protocol server its scheme names, enforces connection limits and
timeouts, and shuts down cleanly. Time comes from an injected `TimeProvider`; transports
come from `Surl.Networking` through the seams in `Surl.Protocol.Abstractions`.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and no protocol server.
Protocol servers receive what it provides through the contracts in Abstractions.
