---
id: BL-116
title: Serve a throwaway certificate only with --self-signed, and refuse a secure scheme without --cert or --self-signed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-107, BL-108]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitTests]
requirement: FR-021
created: 2026-09-29
completed:
---
# BL-116 — Serve a throwaway certificate only with --self-signed, and refuse a secure scheme without --cert or --self-signed

## Goal

`surl https://...` (or any implicit-TLS listen URL) without `--cert` and without
`--self-signed` is refused before any listener binds with ADR-0032's exit code and text;
with `--self-signed` it serves ADR-0010's throwaway certificate as today and writes
ADR-0032's warning on every start.

## Context

FR-021 (as ADR-0032 rewords it); ADR-0032 (BL-100) decisions 9 and 10; ADR-0033 (BL-101)
decision 7 for how the warning is written. BL-108 parses `--self-signed`; BL-107 composed the
log levels this warning goes through.

- `Surl.Console/ServerTlsComposition.cs` `Compose(commandLine, timeProvider)` makes the
  throwaway certificate (`Surl.Networking.UnitLibrary/ThrowawayServerCertificate.cs`) when a
  listen URL is implicit TLS (`TlsSchemes.IsImplicitTls`) and `--cert` is absent; exposes
  `ThrowawayCertificateFingerprint`. `CommandLineRunner.ServeUnderTheLockAsync` calls it and
  `ServeSecuredAsAskedAsync` writes `* Serving a throwaway certificate, SHA-256 <fp>` with
  `-v`.
- Registered implicit-TLS schemes today: `https` (`ImplicitTlsSchemeServer`), `gophers`,
  `mqtts`.
- Conformance tests that start a secure scheme without `--cert` must now pass
  `--self-signed`: `Surl.Conformance.UnitTests/SurlOnLoopback.cs` and
  `UpstreamCurlFetchesFromSurlOverHttpsTests.cs` (and any gophers/mqtts case).
- Tests: `Surl.Console.UnitTests/ServerTlsCompositionTests.cs`,
  `CommandLineRunnerTlsTests.cs`.

## Acceptance criteria

- [ ] `CommandLineRunnerTlsTests` prove, each by name: an `https`, a `gophers` and an `mqtts`
      listen URL with neither `--cert` nor `--self-signed` return ADR-0032's exit code with
      its exact `surl: ` text and bind nothing (the fake listener factory is never called);
      with `--self-signed` they serve a throwaway certificate; with `--cert` they serve the
      file's certificate; `--self-signed` with `--cert` behaves as ADR-0032 decision 10 says.
- [ ] A test proves `--self-signed` writes ADR-0032's warning line at the level ADR-0033
      decision 7 names on every start, and a plain `http` start with `--self-signed` makes no
      certificate (as ADR-0010 says it is made only when needed).
- [ ] Every conformance test that serves a secure scheme passes `--self-signed` or `--cert`,
      and `dotnet test Surl.Conformance.UnitTests` passes with the pinned build.
- [ ] `dotnet build Surl.Console -warnaserror` is clean; the fast tests pass; `Surl.Console`
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
