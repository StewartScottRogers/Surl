---
id: BL-060
title: Parse --cert-type, --key-type and --pass in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-002]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-021
created: 2026-09-28
completed:
---
# BL-060 — Parse --cert-type, --key-type and --pass in Surl.Cli

## Goal

`surl` accepts `--cert-type <PEM|DER|P12>`, `--key-type <PEM|DER>` and
`--pass <phrase>`, carries them on `SurlCommandLine`, lists them in `--help`, and refuses
every combination ADR-0008 section 3 calls `FailedInit` (2).

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0008-the-server-side-tls-contract.md`,
  section 3 (the option table, "Any other `--cert-type` or `--key-type` word", and
  "Consequences": ADR-0007's table gains the three rows through ADR-0008, so no ADR is
  edited here). ADR-0007 row 36 gives the refusal text for a word outside the allowed set:
  `option <name>: is badly used here` (`OptionArgumentReader.BadlyUsed`).
- Where the work lands, all in `Surl.Cli.UnitLibrary`:
  - `CommandLineOptions.cs` - the option table; `--cert`, `--key` and `--cacert` are the
    neighbouring rows (`WithArgument<string>(..., OptionArgumentReader.ReadPath, ...)`).
  - `OptionArgumentReader.cs` - add a reader for the format words beside
    `ReadTlsVersion`, which is the pattern (a lookup that returns `BadlyUsed` on a miss).
  - `SurlCommandLine.cs` - beside `CertificateFile`, `KeyFile`, `CaCertificateFile`.
  - `CommandLineParser.cs` - `Finish` holds the whole-command-line checks; the
    `--tlsv1.x` above `--tls-max` check there is the precedent for the combination
    refusals below.
  - `HelpText.cs` - the help lines, alphabetical by long name, description in column 46.
- The format type: add `public enum CertificateFileFormat { Pem, Der, Pkcs12 }` in
  `Surl.Cli.UnitLibrary` (namespace `Surl.Cli`). `SurlCommandLine` gains
  `CertificateType` (`CertificateFileFormat`, default `Pem`), `KeyType`
  (`CertificateFileFormat`, default `Pem`) and `KeyPassphrase` (`string?`, default
  `null`). BL-012 (`Surl.Networking`) and the composition in `Surl.Console` read them
  later; this task does not load any file.
- Words are matched case-insensitively (ADR-0008: "Case-insensitive, as in curl"):
  `--cert-type` accepts `PEM`, `DER`, `P12`; `--key-type` accepts `PEM`, `DER`. Any other
  word, including curl's `ENG` and `PROV` and `P12` for `--key-type`, is refused.
- `--pass` takes its argument as given, an empty string included (a PKCS#12 file may be
  protected by an empty password); it is not a path, so `ReadPath`'s blank refusal does
  not apply.
- Combination refusals, checked in `Finish` after the whole command line is read, each
  `FailedInit` with the text `option <name>: is badly used here` naming the option that
  cannot be used, followed by the try-help line like every other option refusal:
  `--key` without `--cert` names `--key`; `--key-type` without `--cert` names
  `--key-type`; `--pass` without `--cert` names `--pass`; `--key` with `--cert-type P12`
  names `--key`. When several apply, the first in that order is reported. `--key-type`
  and `--pass` "without `--cert`" means the option was given on the command line, not
  merely that the property holds its default, so the parser records that each was given.
- Out of scope: `--cert-type DER` without `--key`, a wrong or missing passphrase, and
  unreadable files are load-time `CertificateProblem` (58) failures in BL-012, not
  command-line refusals.

## Acceptance criteria

- [ ] `CertificateFileFormat` exists in `Surl.Cli.UnitLibrary` with members `Pem`, `Der`,
      `Pkcs12`, and `SurlCommandLine` has `CertificateType`, `KeyType` and
      `KeyPassphrase` with the defaults above; a `CommandLineParserTests` test asserts
      the three defaults on a command line with only a listen URL.
- [ ] `CommandLineParserTests` pin, for `--cert c.pem` plus each of `--cert-type pem`,
      `--cert-type PEM`, `--cert-type der`, `--cert-type P12`, `--cert-type p12`, the
      resulting `CertificateType`; and for `--cert c.pem --key k.pem` plus
      `--key-type PEM`, `--key-type der`, the resulting `KeyType`.
- [ ] `CommandLineParserTests` pin `FailedInit` (2) with `option --cert-type: is badly
      used here` for `--cert-type ENG`, `--cert-type PROV` and `--cert-type X`, and
      `option --key-type: is badly used here` for `--key-type P12` and
      `--key-type ENG`.
- [ ] `CommandLineParserTests` pin `--pass secret` and `--pass ""` (with `--cert`)
      setting `KeyPassphrase` to `secret` and the empty string, and `--pass` with no
      argument refused with the existing requires-parameter text.
- [ ] `CommandLineParserTests` pin `FailedInit` (2) and the texts above for: `--key k.pem`
      without `--cert`; `--key-type PEM` without `--cert`; `--pass x` without `--cert`;
      `--cert c.p12 --cert-type P12 --key k.pem`.
- [ ] `HelpText` gains, in alphabetical position with the description in column 46,
      exactly these lines, and `HelpTextTests` pins them:
      `     --cert-type <type>                      Format of --cert: PEM, DER or P12 (default PEM)`
      (after `--cert`), `     --key-type <type>                       Format of --key: PEM or DER (default PEM)`
      (after `--key`), and `     --pass <phrase>                         Passphrase for the --key or P12 file`
      (after `--max-time`). The `HelpText` summary is updated to say the text follows
      ADR-0007 section 6 with ADR-0008's three rows.
- [ ] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests
      (`dotnet test --filter "TestCategory!=Integration"`) are green, and
      `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Cli.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
