---
id: BL-682
title: Generate the upstream test certificates from the imported .prm files
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-392]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-682 — Generate the upstream test certificates from the imported .prm files

## Goal

`Surl.Conformance.UnitLibrary` generates the keys and certificates upstream's `tests/certs`
describes (`test-ca`, `test-localhost`, `test-client-cert` and the rest) from the imported `.prm`
files with the BCL's `CertificateRequest`, writes them where a case's `%CERTDIR/certs/...` paths
point, and `UpstreamTestCasesOverHttpTests` uses them in place of BL-392's stand-in CA and
localhost certificate.

## Context

- Governing decision: ADR-0080 (`Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md`).
  Its Context ("Certificates") says `tests/certs` at `curl-8_21_0` holds `.prm` descriptions,
  `test-ca.cnf` and `genserv.pl`, and that 41 references to `%CERTDIR/certs/...` name the generated
  files. Decision 2 ("Where the code lives") puts generating them from the `.prm` files with
  `CertificateRequest` in `Surl.Conformance.UnitLibrary`; decision 2's table serves `https` with the
  generated `test-localhost` certificate. Surl never runs `genserv.pl`, Perl or `openssl`.
- Imported inputs: `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/certs/` -
  `test-ca.cnf`, `test-ca.prm`, `test-client-cert.prm`, `test-client-eku-only.prm`,
  `test-localhost.prm`, `test-localhost.nn.prm`, `test-localhost0h.prm`,
  `test-localhost-san-first.prm`, `test-localhost-san-last.prm`.
- Today: BL-392's test composes `https` with a stand-in CA and localhost certificate generated in
  the test (`Conformance/Surl.Conformance.UnitTests/HttpUpstreamTestCaseServerHalves.cs`,
  `TestCertificateAuthority.cs`). Find every file name runtests' `genserv.pl` produces that cases
  reference (for example `test-ca.crt`, `test-localhost.pem`, `.pub.der`, `.pub.pem`, `.key`,
  client certificate and key) by searching the imported `data/` for `%CERTDIR/certs/`.
- Failing cases this should address (Windows, 2026-11-04): 417 (stdout prints a certificate where
  an empty line is expected), 2033, 2079, 2087 (https: the server half recorded nothing because curl
  closed the TLS connection), probably on certificate files they name (pinned public keys, client
  certificates).
- Library rules: `Surl.Conformance.UnitLibrary` references Abstractions only and uses the BCL only
  (`System.Security.Cryptography.X509Certificates`); file writes go through its existing
  file-access seams so the generator is unit tested without the file system.
- Integration run: `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamTestCasesOverHttp" --logger "console;verbosity=detailed"`.

## Acceptance criteria

- [ ] `Surl.Conformance.UnitLibrary` has a generator that reads the imported `.prm` files and
      produces each certificate and key file the cases name under `%CERTDIR/certs/`, with the
      subject, SAN, key usage, EKU and issuer each `.prm` describes; its unit tests in
      `Surl.Conformance.UnitTests` check those fields per `.prm` and hold it at 100% line and branch
      coverage.
- [ ] `UpstreamTestCasesOverHttpTests` composes `https` with the generated `test-localhost`
      certificate issued by the generated `test-ca`, and writes the generated files under the run's
      `%CERTDIR/certs/`; the stand-in CA and localhost certificate are no longer used by it.
- [ ] In the integration run above, cases 417, 2033, 2079 and 2087 are `pass` in
      `upstream-test-case-results.win-x64.txt`, or Notes give each remaining failure's cause and the
      ID of a task filed for it; no previously passing case now fails.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
