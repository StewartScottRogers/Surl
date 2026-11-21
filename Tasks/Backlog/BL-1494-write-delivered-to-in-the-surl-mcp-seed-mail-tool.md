---
id: BL-1494
title: Write Delivered-To in the surl-mcp seed_mail tool
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1488, BL-1490]
touches: [Surl.Mcp.UnitLibrary, Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1494 — Write Delivered-To in the surl-mcp seed_mail tool

## Goal

The `seed_mail` MCP tool writes `Delivered-To: <to>` ahead of the seeded message when the instance's option is on.

## Context

ADR-0053 Amendment 1 decision 6. File: `Application/Surl.Mcp.UnitLibrary/SeedMailTool.cs` (tests in `StoreToolsTests.cs`); `Application/Surl.Mcp.UnitLibrary/CommandLinePathMembers.cs` lists the command-line options that carry paths, so check whether the new boolean option needs an entry there (it takes no path, so likely not). `surl-mcp` starts servers from command-line style arguments, so the option arrives as `--mail-delivered-to`. When on, the tool prepends one `Delivered-To: <address>` CRLF line for the `to` it was given and writes no `Return-Path` or `Received`. Off: unchanged. Depends on BL-1490 (store property and event member) and BL-1488 (the option). The tool's description text, if it lists behaviour, states the field.

## Acceptance criteria

- [ ] A test starts an instance with the option and `seed_mail` stores a message starting `Delivered-To: <to>` CRLF followed by the given message.
- [ ] A test without the option shows the stored bytes equal the given message.
- [ ] The seeded delivery's event carries `DeliveredFor` with the `to` address, asserted on the event stream the tool exposes.
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean and `Surl.Mcp.UnitTests` fast tests pass at 100% line and branch coverage for the change.

## Notes

## Log

- 2026-11-20: Created.
