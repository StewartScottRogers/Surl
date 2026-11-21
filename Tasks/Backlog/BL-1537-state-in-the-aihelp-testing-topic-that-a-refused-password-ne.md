---
id: BL-1537
title: State in the --aihelp testing topic that a refused password never spends a refuse-login rule
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1537 — State in the --aihelp testing topic that a refused password never spends a refuse-login rule

## Goal

`surl --aihelp testing` tells an agent that a `refuse-login` rule is asked, and spends one of its `times`, only for a login whose credentials matched, so a wrong password neither fires nor spends it.

## Context

The Surly cookbook bug report of 2026-11-21 (batch 8, item 1; tested on Surl `9ad60d80c` with the Curl port as client, so its observation is a lead, not evidence) ran `surl --user alice:s3cret --fault "refuse-login;times=1" ftp://127.0.0.1:2121/`, logged in once with a wrong password (refused, exit 67) and then with the right one, and found the second login refused by the rule. It asked for either behaviour change or documentation.

The behaviour is decided and stays: `Documentation/Planning/Decisions/ADR-0128-how-surl-authentication-publishes-judged-logins-and-applies-refuse-login.md` decision 4 says the fault plan is asked "only once credentials have matched ... A login the credentials already refuse never asks, so it never spends a rule's `times`", and ADR-0124 decision 4 says `times` is decremented when the rule is applied. A wrong-password refusal is the account book's, not the rule's: it publishes `LoginJudgedEvent` with `IsRefusedByFault` false and no `FaultAppliedEvent`.

What is missing is the user-facing statement. The `--fault` paragraph of the `testing` topic is in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` (the `["testing"]` list starting at line 438; the paragraph beginning "`--fault <kind>[;<key>=<value>]`" at line 447). It already explains how an `accept`-stage rule spends its `times`; add the matching sentence for `refuse-login`, for example: "A `refuse-login` rule is asked only once a login's credentials match an account, so a wrong password is refused as usual and neither fires the rule nor spends one of its `times`." No other paragraph, option row or `--manual` text changes.

Stand-in: no store, no new event. The sentence tells a test that seeds `refuse-login;times=N` to count only correct-credential logins, and that a wrong password is told apart on the event stream by `LoginJudgedEvent.IsRefusedByFault` being false.

## Acceptance criteria

- [ ] The `--fault` paragraph of the `testing` topic in `AiHelpProse.cs` states that a `refuse-login` rule is asked only after the credentials match, so a wrong password neither fires it nor spends its `times`, citing nothing but what ADR-0128 decision 4 says.
- [ ] A new test `AiHelpTextTests.Answer_Testing_SaysARefusedPasswordNeverSpendsARefuseLoginRule` in `Application/Surl.Cli.UnitTests` passes and asserts the sentence appears in `surl --aihelp testing`.
- [ ] The existing `--aihelp` completeness and example tests in `Surl.Cli.UnitTests` and `Surl.Console.UnitTests` still pass (`CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` included).
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-11-21: Created.
