---
id: BL-1130
title: Serve the certificate a case's https server line names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1130 — Serve the certificate a case's https server line names

## Goal

When a case's `<server>` line names a certificate after `https` (`https test-localhost0h.pem`), the
HTTPS server half serves that generated certificate instead of `test-localhost`, so upstream tests
311 and 312 pass on CI's Linux and macOS legs and keep passing on Windows.

## Context

- CI run 43629075978 (curl 8.21.0 reference pins): 311 ("HTTPS wrong subjectAltName but right CN",
  `https test-localhost0h.pem`) and 312 ("null-prefixed CN cert", `https test-localhost.nn.pem`)
  fail `errorcode` on Linux and macOS. Both expect exit 60 (certificate verification failed); Surl
  serves the `test-localhost` certificate to every case, which verifies, so curl succeeds. They are on
  the Windows passing list (Schannel fails them for its own reasons).
- At tag `curl-8_21_0` the corpus names five certificates this way: `test-localhost.pem` (14 cases),
  `test-localhost-san-first.pem` (2), `test-localhost-san-last.pem` (2), `test-localhost.nn.pem` (1)
  and `test-localhost0h.pem` (1). `docs/tests/FILEFORMAT.md` at the tag, `<server>`: "Servers that
  require a special server certificate can have the PEM certificate filename (found in the `certs`
  directory) appended to the server name separated by a space."
- `UpstreamTestCertificateGenerator` (`Conformance/Surl.Conformance.UnitLibrary`) already generates
  every `.prm` in `UpstreamTestCases/certs`, the DER `subjectAltName` of `test-localhost0h.prm`
  included, as `NAME.der` and `NAME.key`. `WriteCertificates` in
  `Conformance/Surl.Conformance.UnitTests/UpstreamTestCasesAgainstSurlTests.cs` builds one
  `ServerTlsSettings` from `test-localhost` for the whole run.
- Do: in the library, read the certificate name from a case's `<server>` line (the `.pem` name maps
  to the description `NAME` without `.pem`), with a fast test; in the test project, build a
  `ServerTlsSettings` per named certificate once per run and give a case's HTTPS half the one its
  line names, `test-localhost` when it names none.
- Stand-in: not applicable. This is conformance tooling.

## Acceptance criteria

- [x] A fast test reads `test-localhost0h` from test 311's `<server>` part, `test-localhost.nn` from
      312's, and no name from a case whose line is plain `https`.
- [x] A local Windows run of `UpstreamTestCasesAgainstSurlTests` still passes every case on
      `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`, 311 and 312 included.
- [x] `dotnet build -warnaserror` is clean for both Conformance projects, the fast tests are green,
      and `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Added `UpstreamTestCaseCertificateName.Find` (library, 8 fast tests); the run builds one `ServerTlsSettings` per certificate the corpus names, a case gets the one its https line names, else `test-localhost`. Windows run of `UpstreamTestCasesAgainstSurlTests` passed with no passing-list regressions.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. https server line's named certificate is served; 311 and 312 get their own certificates
