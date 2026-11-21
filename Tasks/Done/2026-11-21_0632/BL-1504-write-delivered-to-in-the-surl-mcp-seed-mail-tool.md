---
id: BL-1504
title: Write Delivered-To in the surl-mcp seed_mail tool
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1498, BL-1500, BL-1515, BL-1503]
touches: [Surl.Mcp.UnitLibrary, Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-21
---
# BL-1504 — Write Delivered-To in the surl-mcp seed_mail tool

## Goal

The `seed_mail` MCP tool writes `Delivered-To: <to>` ahead of the seeded message when the instance's option is on.

## Context

ADR-0053 Amendment 1 decision 6. File: `Application/Surl.Mcp.UnitLibrary/SeedMailTool.cs` (tests in `StoreToolsTests.cs`); `Application/Surl.Mcp.UnitLibrary/CommandLinePathMembers.cs` lists the command-line options that carry paths, so check whether the new boolean option needs an entry there (it takes no path, so likely not). `surl-mcp` starts servers from command-line style arguments, so the option arrives as `--mail-delivered-to`. When on, the tool prepends one `Delivered-To: <address>` CRLF line for the `to` it was given and writes no `Return-Path` or `Received`. Off: unchanged. Depends on BL-1500 (store property and event member) and BL-1498 (the option). The tool's description text, if it lists behaviour, states the field.

## Acceptance criteria

- [x] A test starts an instance with the option and `seed_mail` stores a message starting `Delivered-To: <to>` CRLF followed by the given message.
- [x] A test without the option shows the stored bytes equal the given message.
- [x] The seeded delivery's event carries `DeliveredFor` with the `to` address, asserted on the event stream the tool exposes.
- [x] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean and `Surl.Mcp.UnitTests` fast tests pass at 100% line and branch coverage for the change.

## Notes

- 2026-11-20: The store's Seed publishes DeliveredFor null and nothing exposes whether the option is on to a host, so the tool cannot meet criterion 3 inside Surl.Mcp alone. BL-1515 (MailStore and Composition) adds both; BL-1503 (Surl.Api) must carry the setting to Mail.DeliverAsync first, because Surl.Mcp reaches the store only through Surl.Api.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Backlog. Waiting for BL-1515 (MailStore and Composition: seed DeliveredFor, expose the option) and BL-1503 (Surl.Api Mail.DeliverAsync)
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Lane 6 could not integrate: fast tests failed twice (Surl.Extended.Protocol.DnsQuery.UnitTests: ServeAsync_TcpQueryUnderADelayRule_IsAnsweredOnlyAfterTheDelay; then Surl.Extended.Protocol.DnsQuery.UnitTests: ServeAsync_TcpQueryUnderADelayRule_IsAnsweredOnlyAfterTheDelay) after rebasing onto the other lanes' work. The work is on branch factory/BL-1504-lane-6-20261121-011657; start with git cherry-pick --no-commit factory/BL-1504-lane-6-20261121-011657 and fix it.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. seed_mail Delivered-To with --mail-delivered-to proven by Mcp tests (stored bytes and DeliveredFor event); description states it
