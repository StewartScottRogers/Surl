---
id: BL-108
title: Parse --user, --user-file, --allow-anonymous, --allow-plaintext-auth, --auth and --self-signed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-100, BL-103]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-108 — Parse --user, --user-file, --allow-anonymous, --allow-plaintext-auth, --auth and --self-signed

## Goal

`Surl.Cli` parses `--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth`,
`--auth` and `--self-signed` into `SurlCommandLine` exactly as ADR-0032 decides, with their
help entries in ADR-0034's categories.

## Context

FR-014 and the new rows ADR-0032 (BL-100) adds; ADR-0032 decision 1 gives syntax, argument
kinds, defaults, negatability, repetition and every error text, decision 3 the `--auth`
words; ADR-0034 (BL-102) gives categories and descriptions. BL-103 built the categorised help
table this task adds entries to. Nothing is enforced yet: BL-116 and BL-117 compose these
values, so until then each help description must still be true (ADR-0034 says how an option
that is parsed but not yet composed is described, if at all; follow it).

- `Surl.Cli.UnitLibrary/CommandLineOptions.cs`: the table (`Flag`, `WithArgument<T>`);
  `OptionArgumentReader.cs` (`ReadPath`, `ReadText`); a new reader for `name:password` and
  for the `--auth` method list, each returning ADR-0032's refusal text.
- `Surl.Cli.UnitLibrary/SurlCommandLine.cs`: add the members ADR-0032 names (accounts from
  `--user`, the `--user-file` path as given, the three flags, the accepted-method set).
  Keep a password out of `ToString()` output (a record prints its members): override
  `PrintMembers` or hold accounts in a type whose `ToString` hides the password, and test it.
- Tests: `Surl.Cli.UnitTests/CommandLineParserTests.cs`, `HelpTextTests.cs`.

## Acceptance criteria

- [x] `CommandLineParserTests` prove, for each option, the parsed value and each refusal
      ADR-0032 names (`--user` with no `:`, an empty name, an empty argument, an unknown
      `--auth` word, `=value` on a flag, `--no-` where not negatable), each
      `SurlExitCode.FailedInit` with ADR-0032's exact text and the `try` line.
- [x] Tests prove the repetition rule ADR-0032 decides for `--user` and `--auth`, and that a
      password with a `:` in it (`--user a:b:c`) parses as ADR-0032 says.
- [x] A test proves `new SurlCommandLine { ... accounts ... }.ToString()` does not contain
      the password.
- [x] `HelpTextTests` pin the new options' help lines in ADR-0034's categories.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean; the fast tests pass; 100%
      line and branch coverage kept.

## Notes

- **What landed.** Six rows in `CommandLineOptions`; `SurlCommandLine` gains `Accounts`,
  `UserFile`, `AllowAnonymous`, `AllowPlaintextAuthentication`, `GivenAuthenticationMethods`
  (null when `--auth` is not given), `AcceptedAuthenticationMethods` (that, or the default
  `digest, basic, bearer, aws-sigv4`) and `SelfSigned`. New readers
  `OptionArgumentReader.ReadAccount` and `ReadAuthenticationMethods`.
- **Account type.** `Surl.Cli.CommandLineAccount(UserName, Password)`, with a `ToString` that
  leaves the password out. ADR-0002's layer table lets `Surl.Cli` reference only Core, Output
  and Abstractions, so `Surl.Authentication.Account` cannot be used here; `Surl.Console`
  maps one to the other when BL-117 composes them. It has a different name so a file that
  imports both namespaces is not ambiguous.
- **`--auth` storage.** Stored as the lower-case words in ADR-0032 section 3's order
  (negotiate, ntlm, digest, basic, bearer, aws-sigv4), each word once. That order is
  already the order section 9's warning uses. `Surl.Console` maps a word to
  `Surl.Authentication.AuthenticationMethod`.
  `GivenAuthenticationMethods` records whether `--auth` was given, because section 9 warns
  every time it is given.
- **An empty `--auth` argument** is refused as `is badly used here` and not as `blank`:
  ADR-0032 counts it as one empty item, and an empty item is badly used.
- **Refusal names for `--user`.** `CommandLineOption.ArgumentHoldsSecret` makes each refusal
  name the option as written without its value: `--user`, `--no-user`, and `-u` even inside a
  bundle such as `-vualice`. None of `--user`'s own refusals echoes a password. An unknown
  option is still named as written, so `-xualice:pw` or a misspelled `--usre=a:pw` does echo
  one. ADR-0032 does not cover that case, and it is left as ADR-0007 has it. The duplicate-name refusal
  uses the form of the occurrence that repeats the name.
- **Order of the checks after reading the whole line:** `--tls-max`, then the certificate
  options, then `--self-signed` with `--cert`, then a repeated user name, then no URL.
  User names are compared case-sensitively (ordinal), as `Surl.Authentication.AccountBook`
  compares them.
- **"An empty name" in the first criterion.** ADR-0032 section 1 says an empty name is a Bearer
  token, not a refusal (`--user :tok`). The tests prove that, plus the refusals the ADR does
  name: an empty password (`--user :`), and the empty name given twice.
- **Measured:** Surl.Cli.UnitLibrary 100% line and 100% branch coverage, worst CRAP 10.
  `Finish` was split (`RefuseTlsCombination`) to stay at complexity 10 or less.
  Surl.Cli.UnitTests: 525 tests.
- **Help.** ADR-0034 decision 2's descriptions, categories and defaults. Per decision 2, the
  ADR-0032 options join help with this task. `Explanation` paragraphs (decision 5) are
  BL-123's, so no `Explanation` field was added. The `(warns)` descriptions state intent
  that BL-117 composes.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl parses -u/--user, --user-file, --allow-anonymous, --allow-plaintext-auth, --auth and --self-signed per ADR-0032, with help in ADR-0034's categories
