---
id: BL-547
title: Add the --auth words scram-sha-1 and scram-sha-256 to Surl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-416]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-547 — Add the --auth words scram-sha-1 and scram-sha-256 to Surl.Console

## Goal

`surl --auth scram-sha-256,scram-sha-1 ...` accepts the SASL mechanisms BL-416 added, so a server that
offers SASL through `ISaslAuthenticationPolicy` offers them.

## Context

- BL-416 added `AuthenticationMethod.ScramSha256` and `AuthenticationMethod.ScramSha1` to
  `Surl.Authentication`, outside the default set. `Surl.Console`'s
  `AuthenticationComposition.MethodsByWord` has no word for them yet, so nothing can turn them on.
- The `--auth` option's `--aihelp` facts and allowed values must grow with it (root CLAUDE.md,
  "`surl --aihelp` is how an agent learns the command line").

## Acceptance criteria

- [ ] `AuthenticationComposition.MethodsByWord` maps `scram-sha-1` and `scram-sha-256`, and
      `CommandLineRunnerAuthenticationTests` has a row for each.
- [ ] The `--auth` option's allowed values in `--aihelp` list both words, and the completeness tests pass.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
