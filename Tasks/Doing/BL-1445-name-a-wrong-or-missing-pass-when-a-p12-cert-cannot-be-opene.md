---
id: BL-1445
title: Name a wrong or missing --pass when a P12 --cert cannot be opened
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Application/Surl.Console.UnitTests/CommandLineRunnerTlsTests.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-1445 — Name a wrong or missing --pass when a P12 --cert cannot be opened

## Goal

`surl --cert server.pfx --cert-type P12 --pass wrong https://127.0.0.1:0/` still exits 58, but its
message says the file is a PKCS#12 file that the `--pass` given does not open (or that it needs
`--pass`), instead of `'server.pfx' is not a P12 certificate Surl can read`.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` item 8, Low (Surl `763754785`, unchanged since).
- Cause: `Foundation/Surl.Networking.UnitLibrary/ServerCertificateFileLoader.cs:47-59`. `LoadP12`
  (`:100-118`) calls `X509CertificateLoader.LoadPkcs12Collection(fileBytes, passphrase, ...)`, which
  throws `CryptographicException` for a wrong or missing password. The `catch` at `:56` turns every
  `CryptographicException` into the one "is not a P12 certificate" text.
- Exit code: ADR-0010's table already puts "needs a `--pass` it was not given or was given a wrong one"
  under `CertificateProblem` (58), so only the text changes. A PEM key's missing passphrase is named
  already (`ServerPrivateKeyImport.cs`, "The private key is encrypted and no --pass was given.").
- Telling "a PKCS#12 file" from "not one" without a package: `X509Certificate2.GetCertContentType(byte[])`
  returns `X509ContentType.Pkcs12` for a PFX whatever its password. The exception text differs by
  platform, so never match on it.
- `Application/Surl.Console.UnitTests/CommandLineRunnerTlsTests.cs` pins TLS start messages; update any
  P12 case there.

## Acceptance criteria

- [ ] For a PKCS#12 file and a wrong `--pass`, `TlsFileLoadException.Message` is
      `'<file>' is a P12 file the --pass given does not open.` and the failure stays
      `ServerCertificateUnusable`; with no `--pass` and a password-protected file it is
      `'<file>' is a P12 file that needs --pass.` (named tests in
      `Foundation/Surl.Networking.UnitTests`, generated PFX bytes, platform-neutral).
- [ ] A file that is not PKCS#12 at all keeps `'<file>' is not a P12 certificate Surl can read.`
      (existing test green).
- [ ] `surl` with a wrong `--pass` exits 58 with `surl: (58) ...` carrying the new text (a test in
      `CommandLineRunnerTlsTests`).
- [ ] `dotnet build Foundation/Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests are
      green on every platform, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 8.
- 2026-11-20: Backlog -> Doing.
