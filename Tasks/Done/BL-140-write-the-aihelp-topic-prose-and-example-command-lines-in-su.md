---
id: BL-140
title: Write the --aihelp topic prose and example command lines in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-139]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-035
created: 2026-09-29
completed: 2026-09-29
---
# BL-140 — Write the --aihelp topic prose and example command lines in Surl.Cli

## Goal

Every `--aihelp` topic and the overview carry their hand-written sections - what the topic
covers in depth and the exact example command lines with the exact lines surl writes back -
in the source constant BL-137's ADR names, each statement true of the code on the day it
lands.

## Context

- Decision: BL-137's ADR (expected ADR-0046), decisions 3, 4, 6 and 7 (which examples each
  topic shows and the exact output each one pins).
- What the text must cover (Stewart, 2026-09-29): what surl is and how a command line is
  built (overview); how to read the bound port from `Listening on <scheme>://<host>:<port>/`
  when the listen URL has port 0 (ADR-0007 section 7, FR-006); in-memory versus
  `--directory` and the `.surl` folder and lock, exit 124 (ADR-0031, FR-022 to FR-025);
  accounts, `--user-file` and the login rules (ADR-0032, FR-026 to FR-030); the loosening
  options are for tests only and each writes a `surl: warning:` line at start (ADR-0032
  section 9, ADR-0034 decision 5); log levels, `--trace`, `--log-file` (ADR-0033); the
  limits and their defaults (ADR-0006 section 1, NFR-008 to NFR-016); per protocol, the
  schemes it claims, their default ports (`Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs`), what
  it serves and a pinned upstream curl 8.21.0 command line that reaches it (for example
  `curl mqtt://127.0.0.1:<port>/<topic>`).
- Truth: check each sentence against the code (`Surl.Console/CommandLineRunner.cs`,
  `ListenerStartReporter.cs`, `DataDirectoryLock.cs`, the protocol libraries), not against
  an ADR. `ManualText.cs` already holds checked text for several of these subjects; reuse
  its facts, and where `--aihelp` and `--manual` say the same thing, keep the wording the same.
  Describe only behaviour that exists; the task that later changes it updates the text.
- Any curl command line shown is one pinned upstream curl 8.21.0 accepts, per
  https://curl.se/docs/manpage.html; never the Curl port (ADR-0003).
- Proof that each shown surl output is what `surl` writes is BL-142's; here the examples are
  data the generator places, exposed so BL-142 can run them (the ADR's decision 7 shape).

## Acceptance criteria

- [x] Every topic page and the overview have every hand-written section the ADR prescribes
      filled; no page still holds the ADR's "nothing here" text for a section the ADR
      requires to be written.
- [x] Each example the ADR's decision 7 lists appears exactly as the ADR gives it, in the
      topic it names, and is exposed in the shape BL-142 needs.
- [x] A test in `Surl.Cli.UnitTests` asserts every `--option` named anywhere in the prose or
      examples exists in `CommandLineOptions.All` (as `ManualTextTests` does for the manual),
      and every `surl:` refusal text quoted matches the constant or format the code uses.
- [x] The pinned full outputs in BL-139's tests are updated to the written text and pass.
- [x] Notes lists each statement checked and the file it was checked against.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test
      Surl.Cli.UnitTests --filter "TestCategory!=Integration"` passes; `Surl.Cli.UnitLibrary`
      stays at 100% line and branch coverage.

## Notes

Delivered in `Surl.Cli.UnitLibrary` as ADR-0046 decision 6 names it: `AiHelpProse` (the
overview's `About`, `Command line` and `Conventions`, and every topic's `About`),
`AiHelpExample`, `AiHelpExamplePrecondition` and `AiHelpExamples.All` (decision 7's 19
examples, in its order). `AiHelpText` now writes them in decision 4's shape. Tests:
`AiHelpTextTests` (overview, telnet and exit-codes re-pinned; the About, examples, option-name
and quoted-`surl:`-line tests) and a new `AiHelpExamplesTests` (the 19 in order; a refused
example's lines are what `CommandLineParser` writes; a serving example's lines are
`ListenerStatusLine.Format` of its parsed listen URLs). Surl.Cli: 621 tests pass; 100% line
and branch, highest complexity 10.

Choices (sensible defaults, no ADR needed):
- Consecutive `- ` list items are written without an empty line between them, so they form one
  Markdown list; every other paragraph is separated by one empty line (decision 4 leaves it open).
- Only the `surl:` lines `Surl.Cli` writes are matched against the code in `Surl.Cli.UnitTests`
  (no URL, the `try` line, `option --<name>: <reason>`, `(3) URL rejected`, `(1) Protocol`).
  The lines `Surl.Console` writes are listed in the test and checked below by reading the
  source; running them is BL-142's (decision 9), since `Surl.Cli` cannot see `Surl.Console`.
- The option-name test scans the prose and the examples' surl arguments and output, leaving
  out `curl ...` code spans (their options, such as `--digest`, are curl's). `--no-<name>` counts
  when `<name>` is negatable.
- Example 5 (`--list-directories` with an `http://` URL) is kept exactly as the ADR gives it.
  Its output is true, but the HTTP server answers a directory 404 whatever the option says
  (`HttpProtocolServer.cs` remarks: listing is a later task), so the content page says which
  protocols list (Gopher menus) and that HTTP answers 404, rather than implying HTTP lists.

Statements checked, and where:
- Overview: server-side mate, `--version` names schemes - `ManualText.cs` DESCRIPTION,
  `CommandLineRunner.ComposeVersionText`; reading left to right, first error ends, `--` -
  `ManualText.cs`; `--name=value`, bundles ending at an option with an argument (`-vm30`) -
  `CommandLineParser.cs` (`ReadShortOption`, bundle summary); refusal line and try line, exit 2
  - `CommandLineParser.RefusedOption`, `CommandLineFailure.TryHelpLine`,
  `CommandLineRunner.WriteRefusal`; Ctrl+C/SIGTERM exit 0 - `Surl.Console/Program.cs`.
- listen-urls: syntax, hosts, default and 0 ports - `ManualText.cs` LISTEN URLS,
  `ListenUrlParser.cs`; `(3) URL rejected: <reason>` and `(1) Protocol "<scheme>" not
  supported` - `ListenUrlParser.cs`, `CommandLineRunner.FindListenUrlRefusal`; `(45)` and `(6)` -
  `CommandLineRunner.FormatBindFailure`; the Listening on line - `ListenerStatusLine.Format`;
  UDP for tftp - `ManualText.cs`, `Surl.Protocol.Tftp.UnitLibrary/CLAUDE.md`.
- content: in-memory, `.surl`, lock, 124, 37, 23, MQTT retained file - `ManualText.cs`,
  `DataDirectoryLockOutcome.InUse`, `CommandLineRunner.ServeAsync` (`(37) Could not open
  directory`), `CommandLineRunner.ComposeRetainedMessageFile`; which protocols serve files -
  `CommandLineRunner.ComposeProtocolServers`; HTTP 404 for a directory - `HttpProtocolServer.cs`.
- auth: `--user`, `--user-file`, login rules - `ManualText.cs` ACCOUNTS; name given twice -
  `Surl.Cli.UnitLibrary/CLAUDE.md`; Basic and Bearer refused in clear, Digest not -
  `AuthenticationMethods.SendsPlaintextSecret`, `AuthenticationPolicy.OffersPlaintextSecrets`;
  `--auth` default - `CommandLineOptions.cs`, `AuthenticationMethods.DefaultAccepted`.
- testing/security: the four loosening options, warnings from info up to the log -
  `AuthenticationComposition.WriteLooseningWarnings`, `CommandLineRunner.WriteThrowawayCertificateLines`;
  the six deployment options - `CommandLineOptions.cs` categories; checklist - `ManualText.cs`.
- logging and limits: `ManualText.cs` LOG LEVELS and LIMITS (BL-123-checked);
  `-s` hides failures but not refusals - `CommandLineRunner.HideAtLevelNone`.
- tls: TLS-first schemes - `TlsSchemes.cs` with the registered servers; `(58)` text -
  `CommandLineRunner.FormatMissingCertificate`; 58/77/2 for files - `DescribeTlsFileFailure`;
  `--key`/`--key-type`/`--pass` without `--cert` and `--key` with P12 - `CommandLineParser.FindUnusableCertificateOption`;
  TLS versions - `CommandLineOptions.cs`.
- surl: `-h`/`-M`/`-V` end reading and exit 0 - ADR-0034 decision 4, `CommandLineRunner.RunAsync`;
  `(2) no URL specified` - `CommandLineParser.NoUrlSpecified`.
- dict, gopher, http, mqtt, telnet, tftp: `DictContentDictionary.cs`, `GopherProtocolServer.cs`
  remarks, `HttpProtocolServer.cs` remarks, `Surl.Protocol.Mqtt.UnitLibrary/CLAUDE.md`,
  `TelnetProtocolServer.cs` summary, `Surl.Protocol.Tftp.UnitLibrary/CLAUDE.md`; default ports -
  `SchemeDefaultPorts.cs`. curl command lines use only options in the curl 8.21.0 manpage
  (`-d`, `-I`, `-k`, `-t`, `-T`, `-u`, `--digest`).
- Example texts: as ADR-0046 decision 7 lists them, re-read against the same sources.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --aihelp overview and every topic carry their About prose and ADR-0046's 19 examples, pinned and checked against the parser
