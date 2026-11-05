---
id: BL-616
title: Configure accounts, authentication, TLS and limits through Surl.Api's options
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-615]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-616 — Configure accounts, authentication, TLS and limits through Surl.Api's options

## Goal

`Surl.Api`'s options configure what a test needs the way the command line does - accounts (`--user`,
`--user-file`), `--auth` methods, `--allow-anonymous`, `--allow-plaintext-auth`, `--allow-uploads`, certificates
(`--cert`/`--key`, `--self-signed`, `--cacert`), SSH host keys (`--hostkey`, `--throwaway-hostkey`), and the
connection, time and size limits - each with the command line's default, so nothing is looser than `surl` unless the
caller names the loosening.

## Context

- Decision: BL-611's ADR, decisions 4 and 6 (the typed properties, the option each maps onto, and secure by default).
  Use the property names it decides.
- Each property maps onto the `SurlCommandLine` member the matching option sets (`Application/Surl.Cli.UnitLibrary/
  SurlCommandLine.cs`, `CommandLineOptions.cs`), and goes through `Surl.Composition`'s start, so refusals are the
  command line's: a malformed `--user-file` is 2 naming the line, an unreadable one 37, a bad certificate 58, 2 or 77
  (ADR-0020, ADR-0032, ADR-0051 decision 6). Option limits and defaults: ADR-0006 and ADR-0007's option table.
- Start files are read through `Surl.Composition`'s `readStartFile` seam, so tests give bytes in memory and touch no
  disk. Certificates in tests: copy the approach of `Application/Surl.Console.UnitTests/TestCertificateFiles.cs`;
  nothing Windows-only (root `CLAUDE.md`, "Tests pass on Windows, Linux and macOS").
- The loosening warnings (ADR-0032 section 9) go to the options' log writer, as BL-615 wired it.

## Acceptance criteria

- [ ] Named tests, with a fake listener factory and in-memory start files, show for each property group above that
      it composes the same `SurlCommandLine` member values as the matching `surl` option: accounts, auth methods, the
      three loosening switches, certificate and self-signed, host key and throwaway host key, and each limit.
- [ ] Named tests show the refusals: a malformed accounts file throws with `SurlExitCode.FailedInit` (2) and the
      line named; an unreadable one with exit code 37; `--auth gssapi` without a keytab with 2 and the text
      `--auth gssapi needs --keytab`.
- [ ] Named tests show that options with only a listen URL and an account compose a `SurlCommandLine` with
      anonymous logins, plaintext auth and uploads all off, that the ADR's allow-anonymous property is the only
      thing that turns anonymous logins on, and that setting it writes the `--allow-anonymous` warning
      (ADR-0032 section 9) to the log writer.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
