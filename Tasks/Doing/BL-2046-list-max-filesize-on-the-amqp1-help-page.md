---
id: BL-2046
title: List --max-filesize on the amqp1 help page
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2046 — List --max-filesize on the amqp1 help page

## Goal

The `amqp1` help page lists `--max-filesize`, a limit the AMQP 1.0 container reads.

## Context

Found while working BL-1990 (2026-11-27). In `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs` the help
categories of `max-filesize` do not include `amqp1`, while those of `max-message` do. The container reads
`Limits.MaxUploadBytes` as the link's `max-message-size`
(`Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Links.cs`) and as the
bound on the bytes held in transactions (`Amqp1ContainerConnection.Transactions.cs`). CLAUDE.md's `--aihelp` rule
puts an option on the page of each of its categories; any `amqp1` paragraph in `AiHelpProse.cs` that this changes
moves with it.

## Acceptance criteria

- [ ] `max-filesize` in `CommandLineOptions.cs` has `amqp1` among its help categories.
- [ ] A test in `Application/Surl.Cli.UnitTests` fails if `--max-filesize` is missing from the `amqp1` help page.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
