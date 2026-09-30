---
id: BL-240
title: Add --keytab and compose the Kerberos keytab into Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-239]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-046
created: 2026-09-30
completed:
---
# BL-240 — Add --keytab and compose the Kerberos keytab into Surl.Authentication

## Goal

`surl --keytab <file>` reads an MIT keytab before any listener binds, refuses and warns as
ADR-0057 decision 1 says, is documented in help, the manual and `--aihelp`, and its keys reach
`Surl.Authentication` as a `KerberosAcceptor` on `AuthenticationSettings`, with no authentication
method using it yet.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md`,
  decision 1 (and decision 6 for the library).
  - `--keytab <file>`: category `auth`, description "Read Kerberos service keys from a keytab
    file", the same `OptionArgumentType` (file) as `--user-file`
    (`Surl.Cli.UnitLibrary/CommandLineOptions.cs`, `WithArgument<string>("user-file", ...,
    OptionArgumentReader.Path, ...)`); an empty argument refused while parsing, last value wins,
    not negatable (ADR-0007 section 2).
  - Read when surl starts serving, before any listener binds, as `--user-file` is
    (`Surl.Console/AuthenticationComposition.cs`, `ReadAccounts` and its injected
    `readUserFile`). Refusals, each written after `surl: `:
    - missing, a directory, or unreadable: `SurlExitCode.CouldNotReadFile` (37),
      `(37) Could not read keytab <path>`;
    - not version `0x0502`, or an entry running past the end: `SurlExitCode.FailedInit` (2),
      `(2) Keytab <path> is malformed at byte <offset>`;
    - no entry of an accepted enctype (17, 18, 19, 20): `FailedInit` (2),
      `(2) Keytab <path> holds no key surl can use`.
  - Each skipped entry of another enctype writes
    `surl: warning: --keytab: skipped the <enctype name> key of <principal>` (ADR-0032 section 9's
    form; `<principal>` is `KerberosPrincipalName`'s display form). No key byte is logged at any
    log level.
  - Consistency, after the whole command line is read: `--auth` naming `gssapi` without
    `--keytab` is `FailedInit`, `surl: (2) --auth gssapi needs --keytab`. `--auth negotiate`
    without `--keytab` is unchanged (ADR-0040). `--keytab` when `--auth` names neither `negotiate`
    nor `gssapi` (including when `--auth` is not given: the default set holds neither) is accepted
    with `surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi`.
  - `gssapi` is still refused as `surl: (2) --auth gssapi is not available in this build` until
    BL-218 lands. This task checks `--auth gssapi needs --keytab` first, so `--auth gssapi` alone
    gives the new text and `--auth gssapi --keytab <file>` still gives the not-available text.
  - Help and AI help (ADR-0034, ADR-0046): `--keytab` on the `auth` help page and in the manual
    (`Surl.Cli.UnitLibrary/ManualText.cs`: the file format, the principals of ADR-0057 decision 2
    and the account mapping of decisions 9 and 10); the `auth` topic in
    `Surl.Cli.UnitLibrary/AiHelpProse.cs` gains a `--keytab` paragraph: the file it reads, its
    three refusals and two warnings, that `gssapi` needs it, and - true of this build, since the
    root `CLAUDE.md` requires every statement to be - that no login uses its keys yet (BL-241
    rewrites that sentence to say `negotiate` accepts Kerberos with `--keytab`; BL-218 does the
    same for `gssapi`). The manual says the same. `Surl.Cli.UnitLibrary/AiHelpExamples.cs` gains
    the `auth` example
    `surl --auth negotiate --keytab http.keytab --user-file users.txt http://0.0.0.0:8080/`, run by
    `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`; no
    example reads a file today, so extend that test (in `Surl.Console.UnitTests`) to hand
    `CommandLineRunner`'s injected file reads a hand-written AES keytab for `http.keytab` and a
    one-account `users.txt`, keeping every other example's behaviour.
- Library wiring (ADR-0057 decision 6): `Surl.Authentication.UnitLibrary` gains a
  `ProjectReference` to `Surl.Kerberos.UnitLibrary`, as it references `Surl.Cryptography`.
  `AuthenticationSettings` (`Surl.Authentication.UnitLibrary/AuthenticationSettings.cs`) gains an
  init property carrying the `KerberosAcceptor` (null when no `--keytab`), built in
  `Surl.Console` from the keytab, one `KerberosReplayCache` per process (decision 7), the
  injected `TimeProvider` and a random source implementing `IKerberosRandomSource` over
  `RandomNumberGenerator.Fill`. No authentication method reads it in this task.
- Root `CLAUDE.md`, "`surl --aihelp` is how an agent learns the command line": an option is not
  finished until its facts and topic entries exist.

## Acceptance criteria

- [ ] `CommandLineParserTests` pin: `--keytab http.keytab` sets the keytab path; the last
      `--keytab` wins; `--keytab ""` is refused while parsing with exit 2; `--no-keytab` is an
      unknown option.
- [ ] `CommandLineRunnerAuthenticationTests` (or a new `CommandLineRunnerKeytabTests` in
      `Surl.Console.UnitTests`) pin, with injected file reads and no listener bound in any refused
      case: a missing keytab exits 37 with `surl: (37) Could not read keytab <path>`; a version
      `0x0501` keytab and a truncated one exit 2 with `surl: (2) Keytab <path> is malformed at byte
      <offset>`; a keytab holding only `rc4-hmac` exits 2 with
      `surl: (2) Keytab <path> holds no key surl can use`; a keytab with an AES key and an
      `rc4-hmac` key starts and writes
      `surl: warning: --keytab: skipped the rc4-hmac key of <principal>`; `--auth gssapi` without
      `--keytab` exits 2 with `surl: (2) --auth gssapi needs --keytab`; `--keytab` with the default
      `--auth` starts and writes `surl: warning: --keytab is unused: --auth accepts neither
      negotiate nor gssapi`; `--auth negotiate` without `--keytab` starts with no new warning.
- [ ] A test in `Surl.Console.UnitTests` shows the composed `AuthenticationSettings` carries a
      Kerberos acceptor when `--keytab` is given and null when it is not, and a test asserts that no
      key byte (in hex or Base64) appears in stdout, stderr or the verbose log.
- [ ] `HelpTextTests`, `ManualTextTests`, `AiHelpFactsTests` (including
      `EveryOption_HasAnArgumentTypeAndAllowedValues`), `AiHelpTextTests` (including
      `Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`) and
      `CommandLineRunnerAiHelpTests` (including
      `RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`) pass with `--keytab` described
      "Read Kerberos service keys from a keytab file" in the `auth` category.
- [ ] `Surl.Authentication.UnitLibrary.csproj` references `Surl.Kerberos.UnitLibrary`; every
      existing `Surl.Authentication.UnitTests` test (the ADR-0040 Negotiate tests in
      `NegotiateAuthenticationMethodTests` included) passes unchanged.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror`,
      `dotnet build Surl.Cli.UnitLibrary -warnaserror` and `dotnet build Surl.Console -warnaserror`
      are clean; `dotnet test --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration`; every test is platform-neutral (no drive-letter path).
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch
      coverage, no method over complexity 10 and no CRAP score over 30 for
      `Surl.Authentication.UnitLibrary`, `Surl.Cli.UnitLibrary` and `Surl.Console`.

## Notes

- `Surl.Console` gains a transitive dependency on `Surl.Kerberos.UnitLibrary`; add a direct
  `ProjectReference` only if composition needs a `Surl.Kerberos` type by name (it does, for the
  replay cache and the keytab), and keep it AOT-clean.
- Negotiate behaviour is BL-241; SASL `GSSAPI` is BL-218. Do not change either here.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-217 (ADR-0057 decision 12).
