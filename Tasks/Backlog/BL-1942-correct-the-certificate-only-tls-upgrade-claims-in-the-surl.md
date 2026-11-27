---
id: BL-1942
title: Correct the certificate-only TLS upgrade claims in the Surl.Console mail, FTP and LDAP test class summaries
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Application/Surl.Console.UnitTests/CommandLineRunnerFtpTests.cs, Application/Surl.Console.UnitTests/CommandLineRunnerImapTests.cs, Application/Surl.Console.UnitTests/CommandLineRunnerLdapTests.cs, Application/Surl.Console.UnitTests/CommandLineRunnerPop3Tests.cs, Application/Surl.Console.UnitTests/CommandLineRunnerSmtpTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1942 — Correct the certificate-only TLS upgrade claims in the Surl.Console mail, FTP and LDAP test class summaries

## Goal

The class summary comments of the FTP, IMAP, LDAP, POP3 and SMTP `CommandLineRunner` tests say the TLS upgrade is
offered with a certificate or under TLS-SRP.

## Context

Found while working BL-1934 (2026-11-27), read from source. XML doc comments only; no code, test or behaviour change.

- `ServerTlsComposition.IsTlsUpgradeAvailable` (`Application/Surl.Composition.UnitLibrary`) is a certificate (`--cert`,
  `--self-signed`) or TLS-SRP (ADR-0122 decision 5).
- In `Application/Surl.Console.UnitTests`, each `<summary>` says the upgrade is offered once a certificate is
  configured. Line numbers are as of that date and approximate: `CommandLineRunnerFtpTests.cs` line 13 (`AUTH TLS`),
  `CommandLineRunnerImapTests.cs` line 12 (`STARTTLS`), `CommandLineRunnerLdapTests.cs` line 13 (`StartTLS`),
  `CommandLineRunnerPop3Tests.cs` line 12 (`STLS`) and `CommandLineRunnerSmtpTests.cs` line 13 (`STARTTLS`).
- The expected `(58) ... needs a certificate` strings further down these files are asserted output and stay as they are.

No store, event or fault is involved: comments only.

## Acceptance criteria

- [ ] Each of the five class summaries says its upgrade is offered once a certificate is configured or TLS-SRP is served.
- [ ] The diff to the five files changes only `///` comment lines.
- [ ] `dotnet build Application/Surl.Console.UnitTests -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
