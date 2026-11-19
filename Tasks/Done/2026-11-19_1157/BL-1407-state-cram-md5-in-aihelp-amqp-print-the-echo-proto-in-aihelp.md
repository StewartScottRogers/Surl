---
id: BL-1407
title: State CRAM-MD5 in --aihelp amqp, print the echo .proto in --aihelp grpc, and reword --aihelp webtransport's login sentence
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1398, BL-1411]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1407 — State CRAM-MD5 in --aihelp amqp, print the echo .proto in --aihelp grpc, and reword --aihelp webtransport's login sentence

## Goal

`surl --aihelp amqp` names the mechanisms `amqp://` really offers, `CRAM-MD5` included; `surl --aihelp grpc` prints
the `surl/test/echo.proto` file ADR-0116 decision 1 promises; and `surl --aihelp webtransport` says plainly that a
session needs a login unless `--allow-anonymous` is given.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 5 (amqp, grpc and webtransport parts). Help text only, in
  `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`, after the behaviour exists (BL-1398, BL-1411).
- **amqp** (`AiHelpProse.cs:103`): "PLAIN or AMQPLAIN with an account (`--user`, `--user-file`); `--allow-anonymous`
  also offers ANONYMOUS." The code is right and the help wrong: ADR-0112 decision 2 makes `mechanisms` the policy's
  list, and "by default `amqp://` offers only the challenge mechanisms" - `CRAM-MD5` is in
  `AuthenticationMethods.DefaultAccepted` (`Foundation/Surl.Authentication.UnitLibrary/AuthenticationMethods.cs:17-29`)
  and sends no password, so it is offered without TLS; `PLAIN` and `AMQPLAIN` need TLS or `--allow-plaintext-auth`;
  `--auth` changes the set (see the auth topic). After BL-1411, `--allow-anonymous` offers `PLAIN`, `AMQPLAIN` and
  `ANONYMOUS` on every transport, credentials unchecked.
- **grpc** (`AiHelpProse.cs:134-139`): ADR-0116 decision 1 says the file "served by reflection byte for byte ...
  and printed by `surl --aihelp grpc` (BL-445) so a client can call with a `.proto` file instead of reflection",
  and gives the text (`syntax = "proto3"; package surl.test; message EchoRequest {...} message EchoReply {...}
  service Echo {...}`). The topic names the methods in prose only. Behaviour ADR-0116 promises, so print it
  verbatim, from the ADR, as its own paragraph(s) of the `grpc` topic.
- **webtransport** (`AiHelpProse.cs:362`): "Each `CONNECT` login is judged ... as an HTTP request is: without an
  account a login is refused unless `--allow-anonymous` is given." The "as an HTTP request is" reads as HTTP's
  anonymous-read rule (the cookbook review read it so). After BL-1398 a `CONNECT` with no login is refused
  whenever `--allow-anonymous` is off, account or not; say exactly that.
- Root `CLAUDE.md`, "`surl --aihelp`": no option, server or exit code is added, so the completeness tests need no
  new rows; `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` is untouched as
  no example changes.
- Stand-in: a test author reading `--aihelp` learns which `amqp://` login works with no certificate, can call
  `surl.test.Echo` with a `.proto` file, and knows a WebTransport stand-in needs `--allow-anonymous` or an account.

## Acceptance criteria

- [x] `surl --aihelp amqp` names `CRAM-MD5` as offered over `amqp://` by default, `PLAIN`/`AMQPLAIN` as needing TLS
      or `--allow-plaintext-auth`, and what `--allow-anonymous` offers per BL-1411.
- [x] `surl --aihelp grpc` contains the `.proto` text of ADR-0116 decision 1 line for line.
- [x] `surl --aihelp webtransport` says a `CONNECT` without a login is refused `401` unless `--allow-anonymous` is
      given, with no reference to HTTP's read rule.
- [x] Tests in `Application/Surl.Cli.UnitTests/AiHelpTextTests.cs` pin each of the three.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Cli.UnitLibrary` stays at 100% line
      and branch coverage.

## Notes

- Help text only; webtransport says `401` per the task. The proto is printed as a fenced block from ADR-0116 decision 1.
- BL-1408 corrects the redis, azure-storage and xmpp topics; the board serialises the two.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. aihelp amqp names CRAM-MD5 and anonymous mechanisms, grpc prints echo.proto, webtransport CONNECT login reworded
