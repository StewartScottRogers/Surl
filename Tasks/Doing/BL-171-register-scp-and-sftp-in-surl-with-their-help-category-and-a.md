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
completed:
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

- [ ] A fast `CommandLineRunnerTests` test shows an `scp://` and an `sftp://` listen URL start a
      listener with the SSH server (through `FakeListenerFactory`), and `--version`'s
      `Protocols:` line lists `scp` and `sftp`.
- [ ] Fast tests cover each host-key and authorized-keys start-up refusal the ADR lists, with its
      exit code and text, before any listener binds.
- [ ] `surl --help category` lists the SSH category; `--help <category>` lists every option the
      server reads; `--aihelp <topic>` answers with its `About` and example;
      `CommandLineRunnerAiHelpTests`, `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests`
      pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
