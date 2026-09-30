---
id: BL-171
title: Register scp and sftp in surl with their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-157, BL-158, BL-164, BL-166]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-041
created: 2026-09-29
completed: 2026-09-30
---
# BL-171 — Register scp and sftp in surl with their help category and --aihelp topic

## Goal

`surl scp://127.0.0.1:<port>/` and `surl sftp://127.0.0.1:<port>/` serve the content store with
`SshProtocolServer`, its host key and authorized keys loaded from BL-158's options, and `surl
--help` and `--aihelp` list the SSH protocol category and topic.

## Context

- Decisions: BL-154's ADR (host-key options, start-up refusals and exit codes, the category
  name), ADR-0034 decision 1 ("How a protocol's category joins": the registering task adds the
  category row and adds it to every option the server reads), ADR-0046 decision 3 (the topic's
  `About` text and example in the same change; `CommandLineRunnerAiHelpTests` fail until then).
- Code: `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` (add the server once, both
  schemes) and the start-up order of ADR-0031 decision 7 (probe, scheme check, lock, load state,
  bind) - read the host-key and authorized-keys files there, before any listener binds, turning
  the parsers' refusals (BL-157, BL-160) into the ADR's exit codes and texts;
  `Surl.Console/AuthenticationComposition.cs` for the policy; remove BL-158's "not available in
  this build" refusal. `Surl.Cli.UnitLibrary/HelpCategories.cs`, `CommandLineOptions.cs`
  (categories of `--allow-uploads`, `--list-directories`, `--follow-symlinks`,
  `--serve-dot-files`, `--max-message`, `--max-filesize`, `--head-timeout`, `--user`,
  `--user-file`, `--allow-anonymous` and the SSH options), `AiHelpProse.cs`, `AiHelpExamples.cs`,
  `ManualText.cs`.
- `--allow-weak-ssh-algorithms` (BL-221): pass `SurlCommandLine.AllowWeakSshAlgorithms` as
  `SshAlgorithmOffer.Default`'s `allowWeakAlgorithms` (which also sets the offer's
  `AllowsWeakAlgorithms`, so the server accepts `ssh-rsa`/`ssh-dss` user signatures and RSA user
  keys under 2048 bits) and as `SshHostKeyFile.Read`'s `allowWeakAlgorithms`.
- Keep this task to wiring; any server behaviour found wrong becomes a `Surl.Protocol.Ssh` task
  filed by `task-planner`.

## Acceptance criteria

- [x] A fast `CommandLineRunnerTests` test shows an `scp://` and an `sftp://` listen URL start a
      listener with the SSH server (through `FakeListenerFactory`), and `--version`'s
      `Protocols:` line lists `scp` and `sftp`.
- [x] Fast tests cover each host-key and authorized-keys start-up refusal the ADR lists, with its
      exit code and text, before any listener binds.
- [x] `surl --help category` lists the SSH category; `--help <category>` lists every option the
      server reads; `--aihelp <topic>` answers with its `About` and example;
      `CommandLineRunnerAiHelpTests`, `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests`
      pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Built directly from ADR-0051 decisions 4, 6, 8, 11 and 12 (fully specified), without a separate
  architect plan. `SshProtocolServer` is registered once in `ComposeProtocolServers` for `scp` and
  `sftp`, with the host keys, `SshAlgorithmOffer.Default(keys, AesGcm.IsSupported)`, the one
  `AuthenticationPolicy` as its `ISshAuthenticationPolicy`, `SshSystemRandomSource` and the content
  store. New `Surl.Console/SshHostKeyComposition.cs` reads `--hostkey` files, makes the throwaway
  key, and writes the warning and notes; `AuthenticationComposition` reads `--authorized-keys`
  files into `AuthenticationSettings.AuthorizedKeys`. The runner's `readUserFile` seam became
  `readStartFile`, since it now reads all three kinds of file.
- Start order: probe, scheme check, 58 (no certificate), 2 (SSH URL with no host key), then the
  `--user-file`, the `--authorized-keys` files and the `--hostkey` files, then the lock. Host keys are
  read whenever given, SSH URL or not (decision 4); the throwaway key is made only when an `scp` or
  `sftp` URL is served, and the verbose host-key notes are written only then too.
- Choice: the host-key note carries the `* ` prefix every verbose note has
  (`* Serving SSH host key ssh-rsa, --hostpubsha256 ... --hostpubmd5 ...`), as ADR-0020 did for the
  throwaway-certificate note ADR-0010 wrote without it.
- Choice: `--allow-weak-ssh-algorithms` stays refused as not available in this build, beside
  `--hostcert` (until BL-222, as ADR-0051 says): BL-221, which makes the server offer the weak
  algorithms, was still in Doing when this was built, and decision 11's warning would claim algorithms not offered.
  BL-250 lifts it now that BL-221 is done (the Context line above is its wiring). The host-key reader already receives the option.
- Choice: the `ssh` category also holds `--directory` and `--pass` (the SSH server serves the
  content store, and `--pass` decrypts `--hostkey` keys), besides the task's list. The `--aihelp`
  example is the refused `sftp://` start without a host key: a serving example would print the
  throwaway key's random hash, which `CommandLineRunnerAiHelpTests` compares byte for byte.
- `AiHelpTextTests.Answer_EveryPage_NamesOnlyOptionsThatExist` now allows `--hostpubsha256` and
  `--hostpubmd5` in surl's own lines: they are curl's options, named by the note and warning.
- `Topics_AreTheAdrsSixteenInOrdinalOrder` keeps its name (now eighteen topics) because the root
  `CLAUDE.md`, outside this task's touches, names it.
- ADR-0051 could not be edited here (`Documentation/Planning/Decisions` is outside the touches);
  BL-249 records the choices above there.
- Measured: `Measure-CodeQuality.ps1 -Library Surl.Console` 100% line, 100% branch, 0 failing
  (worst CRAP 10); `-Library Surl.Cli.UnitLibrary` 0 failing. Fast tests: Surl.Cli.UnitTests 694,
  Surl.Console.UnitTests 300, all projects green. No upstream curl measurement: nothing here changes
  bytes on the wire; BL-172 proves the transfers against the pinned builds.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Filed BL-250 (serve `--allow-weak-ssh-algorithms` after BL-221) and BL-249 (record
  this task's choices in ADR-0051).
- 2026-09-30: Doing -> Done. surl serves scp:// and sftp:// with SshProtocolServer, reading --hostkey and --authorized-keys files at start with ADR-0051's refusals, with the ssh help category and --aihelp topic
