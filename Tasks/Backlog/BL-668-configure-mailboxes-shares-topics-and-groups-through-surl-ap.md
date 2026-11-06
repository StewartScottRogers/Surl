---
id: BL-668
title: Configure mailboxes, shares, topics and groups through Surl.Api's options and a config file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-616, BL-644, BL-654, BL-663, BL-666]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-668 — Configure mailboxes, shares, topics and groups through Surl.Api's options and a config file

## Goal

A .NET test suite configures a `Surl.Api` server's mail domains, mailboxes, shares, share access, buckets, topics,
queues, topic maps, topic access and groups - and, if BL-641's ADR says so, a config file - through the same
configuration model the command line fills, with the same defaults, refusals and warnings.

## Context

- Decisions: BL-611's ADR (`Surl.Api`'s surface: options mapped onto the one model), BL-641's ADR (the model's
  layer-2 types; whether `Surl.Api` takes a config file path), BL-642's ADR (meanings).
- Builds on: BL-615/BL-616 (`Surl.Api` and its options for accounts, authentication, TLS and limits), BL-644 (config
  file), BL-653 and BL-654 (mail), BL-663 (files), BL-666 (messages), BL-646 (groups, via BL-654).
- Where: `Application/Surl.Api` and `Application/Surl.Api.UnitTests`. No new configuration logic here: each
  property maps onto the model `Surl.Composition` already reads, so an API-configured server and a
  command-line-configured server are composed by the same code.
- Secure by default (ADR-0032): every property's default equals the option's default; a widening property is named
  after its option and produces the same warning text through the API's log.

## Acceptance criteria

- [ ] `SurlServerOptions` (or the name BL-611's ADR gives) has a typed property per layer-2 option, each with XML
      documentation naming the option it mirrors.
- [ ] Named tests start a server in-process configured with a mail domain and a mailbox, a share with access and a
      topic grant, and show a refused configuration surfacing the same exit code and text as `surl` writes, through
      the API's start exception.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Api` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
