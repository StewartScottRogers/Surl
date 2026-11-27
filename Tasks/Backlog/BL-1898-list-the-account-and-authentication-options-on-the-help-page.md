---
id: BL-1898
title: List the account and authentication options on the help pages of every server that reads them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1898 — List the account and authentication options on the help pages of every server that reads them

## Goal

`surl --help <topic>` and `surl --aihelp <topic>` list each account and authentication option on every server topic whose server reads it.

## Context

- Found by BL-1891, by reading `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs`.
- The categories of `--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth` and `--auth` name neither `kafka` nor `xmpp`, though both servers log in with SASL under those options (their `AiHelpProse` paragraphs say so). `--allow-plaintext-auth` does not name `amqp`, though that paragraph names it too.
- Check each server in `Surl.Composition`'s `ProtocolServerRegistrations.Original` and `ExtendedProtocolServers.Registrations` for the options it reads, `mqtt5`, `redis` and the other extended ones included.

## Acceptance criteria

- [ ] `surl --help kafka` and `surl --help xmpp` list `--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth` and `--auth`.
- [ ] `surl --help amqp` lists `--allow-plaintext-auth`.
- [ ] Every other server topic lists each of those five options its server reads, as the code shows, and none it does not.
- [ ] A named test in `Surl.Cli.UnitTests` pins the lists.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
