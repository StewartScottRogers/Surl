---
id: BL-1539
title: Percent-encode only the item grammar's delimiters in SurlFaultRule.Item and list_faults
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1539 — Percent-encode only the item grammar's delimiters in SurlFaultRule.Item and list_faults

## Goal

`server.Faults.List()` and `surl-mcp`'s `list_faults` give a rule's item back readable, as the control endpoint's `GET /v1/faults` does: `error;scheme=http;path=/report.txt;reply=503` rather than `error;scheme=http;path=%2Freport.txt;reply=503`.

## Context

The Surly cookbook bug report of 2026-11-21 (batch 8, item 4; Surl `9ad60d80c`, the Curl port as client) added `error;scheme=http;path=/report.txt;reply=503;times=2` and got `error;scheme=http;path=%2Freport.txt;reply=503` back from `list_faults` and `SurlFaultRule.Item`.

Two parts, one of them intended:
- **`times` left out: intended, keep it.** `SurlServerFaults.List` (`Application/Surl.Api/SurlServerFaults.cs:125`) spells each rule with `SurlFault.WithoutCountFrom(rule)` (`Application/Surl.Api/SurlFault.cs:99`) on purpose, so a spent rule still spells an item `surl` takes (`times=0` is out of range); `SurlFaultRule.Remaining` and `list_faults`' `remaining` carry the count. This is stated in `Application/Surl.Api/SurlFaultRule.cs`, `Application/Surl.Api/CLAUDE.md` and the `list_faults` description (`Application/Surl.Mcp.UnitLibrary/ListFaultsTool.cs:20`). Do not change it.
- **Every value percent-encoded: the defect.** `SurlFault.AddKey` (`Application/Surl.Api/SurlFault.cs`, the private method at the end of the file) writes each value through `Uri.EscapeDataString`, which encodes `/`, `:`, space and every other reserved character. The item grammar (ADR-0101 decision 3, "Escaping is percent-encoding") splits only at `;`, `,` and `=` and decodes `%`, and refuses a control character. The control endpoint already writes exactly that: `FaultItemText.Encode` (`Application/Surl.Composition.UnitLibrary/FaultItemText.cs:57`) encodes `;`, `,`, `=`, `%` and `char.IsControl` characters as their UTF-8 bytes in `%XX` form and leaves the rest. Two hosts spelling one rule two ways breaks "configuration as data" (root `CLAUDE.md`).

Change `SurlFault.AddKey` to encode as `FaultItemText.Encode` does. `FaultItemText` is internal to `Surl.Composition`; write the same rule in `Surl.Api` (a private encoder with a comment naming ADR-0101 decision 3 and `FaultItemText`) rather than widening `Surl.Composition`. `ToItem` also composes the `--fault` words `SurlServerArguments.Compose` passes at start (`Application/Surl.Api/SurlServerArguments.cs:168`) and `SurlServerFaults.Add` parses (`SurlServerFaults.cs:99`); the reader decodes either spelling, so every rule still reads back the same. `surl-mcp` needs no edit: `ListFaultsTool` writes `rule.Item` as it is.

Tests that pin the old spelling and change with it, all in `Application/Surl.Api.UnitTests`: `SurlServerArgumentsTests.Faults.cs` (lines 14 and 54), `SurlServerTests.FaultList.cs` (lines 35 and 36), `SurlServerTests.ProtocolServerErrors.cs` (lines 22, 83 and 101) and `SurlServerTests.ProtocolServers.cs` (line 62). Update the examples in the XML doc comments of `SurlFault.ToItem` (line 117) and `SurlFaultRule` (line 11).

Stand-in: no store and no event change. A test reading `Faults.List()` or `list_faults` can now compare the item with what it added (less `times`), on every host alike.

## Acceptance criteria

- [ ] `SurlFault.ToItem` encodes only `;`, `,`, `=`, `%` and control characters, as `FaultItemText.Encode` does; a new test `SurlFaultTests.ToItem_EncodesOnlyTheItemGrammarsDelimiters` pins `error;scheme=http;path=/a b%3Dc%3B%25;reply=503 Busy%3B later` for path `/a b=c;%` and reply `503 Busy; later`, and a value with a control character encodes it as `%XX`.
- [ ] `SurlServerTests` has a test that adds `error;scheme=http;path=/report.txt;reply=503;times=2` and reads `List()` back as `Item` `error;scheme=http;path=/report.txt;reply=503` with `Remaining` 2.
- [ ] Every rule the existing fault tests add still reads back as the same `FaultRule` through `CommandLineParser` (the round-trip tests in `SurlServerArgumentsTests.Faults.cs` pass with their new expected text).
- [ ] The doc comments on `SurlFault.ToItem` and `SurlFaultRule` show the new spelling and still say `times` is left out and `Remaining` carries it.
- [ ] Coverage of `SurlFault.cs` stays 100% line and branch; `dotnet build Application/Surl.Api -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green, `Surl.Mcp.UnitTests` included.

## Notes

## Log

- 2026-11-21: Created.
