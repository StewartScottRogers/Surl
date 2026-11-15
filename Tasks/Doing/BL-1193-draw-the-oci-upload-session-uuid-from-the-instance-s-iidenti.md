---
id: BL-1193
title: Draw the OCI upload session UUID from the instance's IIdentifierRandom so --random-seed repeats it
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1156]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-064
created: 2026-11-15
completed:
---
# BL-1193 — Draw the OCI upload session UUID from the instance's IIdentifierRandom so --random-seed repeats it

## Goal

The OCI upload session UUID comes from the instance's IIdentifierRandom, so two instances with the same RandomSeed hand out the same UUID for the same exchange order.

## Context

ADR-0163 classes the OCI upload session UUID as a seedable identifier and amends ADR-0130 decision 7. It is `Guid.NewGuid` today (`NewRandomSessionId` in `OciRegistryProtocolServer.cs`); every `/v2/` request still needs the `OciTokenBook` bearer token. The UUID stays a version 4 UUID in form (version and variant bits set), its other bits drawn from the seedable source.

## Acceptance criteria

- [ ] The session UUID is built from IIdentifierRandom bytes with the version 4 and RFC 4122 variant bits set; `Guid.NewGuid` is no longer the production default.
- [ ] A test shows two instances with the same RandomSeed give the same UUID for the same exchange order.
- [ ] An unseeded instance still gives a fresh UUID each time.
- [ ] The internal constructor that takes a `Func<Guid>` stays for tests.
- [ ] dotnet build is clean and the fast tests pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
