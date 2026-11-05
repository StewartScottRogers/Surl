---
id: BL-804
title: Compose protocol servers from an explicit registration list in Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-804 — Compose protocol servers from an explicit registration list in Surl.Composition

## Goal

`Surl.Composition` builds its protocol servers from an explicit, ordered registration list - one entry per server
naming its schemes and how it is built from the composed services - so a new protocol, or a test's own server
(BL-813), joins by adding an entry rather than editing a central method, with AOT-safe registration and the same
servers in the same order as today.

## Context

- Decision: BL-822's ADR decision 2 (the registration entry's shape - planning name `ProtocolServerRegistration`
  with the schemes and a factory over the composed services - and where the list lives).
- Today: `SurlServerComposition.ComposeProtocolServers` (`Application/Surl.Composition.UnitLibrary/
  SurlServerComposition.cs`) builds every original server inline in one array, with `ImplicitTlsSchemeServer`
  second registrations for `https`, `imaps`, `ldaps`, `pop3s`, `smtps` and `wss`; `ExtendedProtocolServers.Compose`
  does the same for the extended servers (each "joining with one line"); `ComposeUnservedProtocolServers` builds
  them only for their schemes.
- Keep the method name `ComposeProtocolServers` and its result (ADR-0099 decision 1: many Backlog tasks and root
  `CLAUDE.md`'s `--aihelp` rule name it); its body iterates the list. No reflection, no assembly scanning (native
  AOT, root `CLAUDE.md`). Extended servers keep `ExtendedProtocolServers` unless the ADR moves them onto the same
  list; if it does, do it here.
- Lanes are adding extended servers concurrently ("Serve X from the surl executable" tasks): after rebasing,
  convert any entry they added.
- Stand-in: the open-for-extension principle for protocols. No events or faults here.
- Amended by ADR-0124 decision 2: the entry is `ProtocolServerRegistration(IReadOnlyList<string> Schemes,
  Func<ComposedProtocolServices, IProtocolServer> Create)`; the originals are `ProtocolServerRegistrations.Original`;
  the extended servers **do** move onto the same record, in `ExtendedProtocolServers.Registrations`, still gated
  by `SURL_EXTENSIONS`; and the composition gains an input `AdditionalProtocolServers` (registrations, each with
  its already-parsed `ListenUrl`s) that `Surl.Api` fills for a test's own server (BL-813).

## Acceptance criteria

- [ ] `ComposeProtocolServers` returns the same server types, schemes and order as before this task (a named test
      pins the scheme order and the implicit-TLS pairs), built from the registration list.
- [ ] A named test shows a registration list with one extra test-only entry composes that server beside the
      originals without any other code change.
- [ ] `surl --version`'s `Protocols:` line and the `--aihelp` completeness tests
      (`CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic`) are unchanged and
      green.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): extended servers on the same record; `AdditionalProtocolServers` input.
