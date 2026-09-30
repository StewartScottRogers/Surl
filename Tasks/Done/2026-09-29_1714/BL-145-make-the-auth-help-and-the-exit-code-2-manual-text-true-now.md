---
id: BL-145
title: Make the --auth help and the exit code 2 manual text true now every --auth method is served
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-145 — Make the --auth help and the exit code 2 manual text true now every --auth method is served

## Goal

`surl --help --auth`, `surl --help testing` and `surl --manual` say only what surl does: every
`--auth` word is served, and an `--auth` word outside the list is refused as an option value, not
as "a method this build does not have".

## Context

Found during BL-124. Three texts in `Surl.Cli.UnitLibrary` are stale since AWS Signature Version 4
was composed (BL-122, BL-136, ADR-0045):

1. `Surl.Cli.UnitLibrary/CommandLineOptions.cs`, `AuthExplanation` (about line 36), the paragraph
   `surl --help --auth` and `surl --help testing` print, says: "This build checks basic, bearer,
   digest, ntlm and negotiate, and refuses to start when --auth names another." That is false:
   `surl --auth aws-sigv4 http://127.0.0.1:8080/` starts and serves. `OptionArgumentReader.
   AuthenticationMethodWords` accepts all six words (`negotiate`, `ntlm`, `digest`, `basic`,
   `bearer`, `aws-sigv4`) and `Surl.Console/AuthenticationComposition.cs` `MethodsByWord` maps
   every one of them to an `AuthenticationMethod`, so no listed word is refused at start. A word
   not in the list is refused by `OptionArgumentReader.ReadAuthenticationMethods` while the command
   line is read, which is exit code 2 (`SurlExitCode.FailedInit`, "an option refused").
2. `Surl.Cli.UnitLibrary/ManualText.cs`, EXIT CODES, code 2 (about line 174): "an option refused,
   an --auth method this build does not have, a malformed --user-file, or a --cacert file that does
   not exist". No `--auth` method is missing from this build any more, so the middle clause goes;
   a word outside the list is already covered by "an option refused".
3. `Surl.Cli.UnitLibrary/CommandLineAccount.cs`, the type's `<summary>`: "`Surl.Console` is to hand
   these to `Surl.Authentication` once BL-117 composes them; until then nothing reads them." BL-117
   is done: `Surl.Console/AuthenticationComposition.cs` reads every `CommandLineAccount` into the
   account book. Only the doc comment changes.

The texts are pinned word for word: `AuthExplanationLines` in `Surl.Cli.UnitTests/HelpTextTests.cs`
(about line 51, used by the `--help testing` and `--help --auth` tests) and the EXIT CODES lines in
`Surl.Cli.UnitTests/ManualTextTests.cs` (about line 183). Keep the rest of the `--auth` paragraph
(the list, the default `basic,bearer,digest,aws-sigv4`, why ntlm and negotiate are not in the
default, the startup warning) as it is; ADR-0032 section 3 and ADR-0045 are the sources. No upstream
curl behaviour is claimed or changed, so no measurement is needed. The uncommitted BL-124 edits to
`Surl.Cli.UnitLibrary/CLAUDE.md` are not this task's.

## Acceptance criteria

- [x] `AuthExplanation` in `Surl.Cli.UnitLibrary/CommandLineOptions.cs` no longer contains
      "refuses to start" nor claims any listed method is unchecked; it states that every one of the
      six listed methods is checked, and that a word outside the list is refused (exit code 2).
- [x] The EXIT CODES entry for code 2 in `Surl.Cli.UnitLibrary/ManualText.cs` no longer contains
      "an --auth method this build does not have"; it still names an option refused, a malformed
      --user-file and a --cacert file that does not exist.
- [x] The `<summary>` of `CommandLineAccount` in `Surl.Cli.UnitLibrary/CommandLineAccount.cs` no
      longer mentions BL-117 or "nothing reads them", and says `Surl.Console`'s
      `AuthenticationComposition` hands the accounts to `Surl.Authentication`.
- [x] `AuthExplanationLines` in `Surl.Cli.UnitTests/HelpTextTests.cs` and the EXIT CODES lines in
      `Surl.Cli.UnitTests/ManualTextTests.cs` pin the new wording, and
      `grep -rn "refuses to start when --auth\|this build does not have" --include=*.cs .` finds
      nothing outside `bin/` and `obj/`.
- [x] The new wording matches what the parser tests already pin, unchanged and passing:
      `Parse_Auth_IsTheAcceptedMethodSet` ("Every word" row, all six words accepted) and
      `Parse_AuthBadWord_IsBadlyUsed` ("An unknown word" row, `--auth kerberos` refused with
      "option --auth: is badly used here", which is `SurlExitCode.FailedInit` = 2) in
      `Surl.Cli.UnitTests/CommandLineParserTests.cs`. The text says exit code 2 for an unknown word
      only because those tests prove it.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Surl.Cli.UnitTests --filter "TestCategory!=Integration"` passes; no new test
      needs `TestCategory=Integration`.

## Notes

BL-146 (naming and doc-comment alignment) waits on this task because both change
`Surl.Cli.UnitLibrary`.

Delivered directly rather than through the full /feature stages: three stale sentences and their
pins, no behaviour change and no upstream curl claim. Wording chosen: "surl checks every one of
the six, and refuses a word outside the list as an option badly used (exit code 2)" - "badly used"
echoes the parser's own "option --auth: is badly used here", pinned by
`Parse_AuthBadWord_IsBadlyUsed`. Help wraps at 79 columns, so the lines after the new sentence
rewrapped. Fast tests: Surl.Cli.UnitTests 639/639, whole solution green (the SMB test scaffold
matches no fast test, which makes `dotnet test` exit 255 with no failure; pre-existing).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --help --auth, --help testing and --manual now say every --auth method is checked and an unknown word is exit code 2
