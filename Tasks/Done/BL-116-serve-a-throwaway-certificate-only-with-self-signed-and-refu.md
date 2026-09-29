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
completed: 2026-09-29
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

- [x] `CommandLineRunnerTlsTests` prove, each by name: an `https`, a `gophers` and an `mqtts`
      listen URL with neither `--cert` nor `--self-signed` return ADR-0032's exit code with
      its exact `surl: ` text and bind nothing (the fake listener factory is never called);
      with `--self-signed` they serve a throwaway certificate; with `--cert` they serve the
      file's certificate; `--self-signed` with `--cert` behaves as ADR-0032 decision 10 says.
- [x] A test proves `--self-signed` writes ADR-0032's warning line at the level ADR-0033
      decision 7 names on every start, and a plain `http` start with `--self-signed` makes no
      certificate (as ADR-0010 says it is made only when needed).
- [x] Every conformance test that serves a secure scheme passes `--self-signed` or `--cert`,
      and `dotnet test Surl.Conformance.UnitTests` passes with the pinned build.
- [x] `dotnet build Surl.Console -warnaserror` is clean; the fast tests pass; `Surl.Console`
      keeps 100% line and branch coverage.

## Notes

- Decisions (no new ADR needed; ADR-0032 sections 9-10 and ADR-0033 section 7 fix every byte):
  - The refusal runs in `CommandLineRunner.ServeAsync` right after the unregistered-scheme check
    and before the data-directory lock, the retained-message load and any listener: a server that
    cannot start should not take the lock. `ServerTlsComposition.FindListenUrlWithoutCertificate`
    names the first implicit-TLS listen URL in command-line order.
  - `<listen url>` in the `(58)` text is `ListenerStatusLine.FormatBoundListenUrl` with the port
    as given (0 stays 0), since nothing is bound yet; `-s` hides it (ADR-0033 section 1).
  - The warning goes to the log stream (stderr or `--log-file`) at `info` and above, before the
    `-v` fingerprint note, unstamped. It is the only ADR-0032 section 9 warning surl writes yet;
    the other three will slot in before it as their tasks land.
  - `--self-signed` with `--cert` is refused by `Surl.Cli`'s parser (BL-108); a runner test pins
    that refusal end to end.
- `ServeAsync` reached Cobertura complexity 12, so the listen-URL refusals
  (`FindListenUrlRefusal`) and the lock choice (`TakeDataDirectoryLockWhenGiven`) were extracted.
- Verified: `dotnet build -warnaserror` clean; fast tests green; `Surl.Console.UnitTests` 158 and
  `Surl.Conformance.UnitTests` 109 (pinned build) pass; `Measure-CodeQuality.ps1 -Library
  Surl.Console`: 100% line, 100% branch, worst CRAP 10, 0 failing.
- `Surl.Console/CLAUDE.md` updated (inside `Surl.Console`).
## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. https, gophers and mqtts without --cert or --self-signed are refused with (58) before binding; --self-signed serves the throwaway certificate and warns on every start
