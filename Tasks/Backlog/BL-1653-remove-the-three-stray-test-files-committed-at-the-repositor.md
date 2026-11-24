---
id: BL-1653
title: Remove the three stray test files committed at the repository root by BL-1529
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [CommandStageFaultsTests.cs, InMemoryConnectionTests.cs, PeekContractTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-1653 — Remove the three stray test files committed at the repository root by BL-1529

## Goal

The repository root holds no stray `.cs` test files; the tests they resemble live only in
their project.

## Context

Commit `efd200fea` (BL-1529) added `CommandStageFaultsTests.cs` (0 bytes),
`PeekContractTests.cs` (0 bytes) and `InMemoryConnectionTests.cs` (a 57-line fragment of
`Foundation/Surl.Protocol.Abstractions.UnitTests/InMemoryConnectionTests.cs`) at the
root. No project compiles them, so they mislead a reader into thinking tests live there.
`MSTestSettings.cs` at the root is intended (shared assembly settings) and stays.

## Acceptance criteria

- [ ] The three files are deleted with `git rm`.
- [ ] Every test method in the root `InMemoryConnectionTests.cs` exists in
      `Foundation/Surl.Protocol.Abstractions.UnitTests/InMemoryConnectionTests.cs` (check
      before deleting; move any that do not).
- [ ] `dotnet build` clean and `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

## Log

- 2026-11-24: Created.
