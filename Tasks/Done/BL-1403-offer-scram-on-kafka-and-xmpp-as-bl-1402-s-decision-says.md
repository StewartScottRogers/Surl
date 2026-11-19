---
id: BL-1403
title: Offer SCRAM on kafka and xmpp as BL-1402's decision says
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1402]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1403 — Offer SCRAM on kafka and xmpp as BL-1402's decision says

## Goal

With an account and no `--auth`, `kafka://` answers SaslHandshake with the mechanism list BL-1402's ADR states, and
`xmpp` offers SCRAM as the same ADR states, so a SCRAM-SHA-256 login succeeds over plain text without
`--allow-plaintext-auth`.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 4, and finding 2's `PLAIN`-only log on XMPP. BL-1402 records
  the decision; the option it weighs first is a per-scheme default that adds SCRAM for `kafka`/`kafkas` and
  `xmpp`/`xmpps` when `--auth` is not given.
- Where the default comes from today: `Application/Surl.Composition.UnitLibrary/AuthenticationComposition.cs:166-174`
  (`ComposeSettings`: the `--auth` words, else `AuthenticationMethods.DefaultAccepted`) and the per-scheme policy
  `ComposedProtocolServices.AuthenticationPolicyFor(scheme)` (`ComposedProtocolServices.cs:110`). The Kafka broker
  (`ExtendedProtocolServers.ComposeKafkaBroker`: `services.AuthenticationPolicyFor("kafka")`) and the XMPP server
  (`ExtendedProtocolServers.cs:186-193`: `AuthenticationPolicyFor("xmpp")`) are composed with it.
  `KafkaSaslHandshakeAnswerer` and `XmppServerConnection` already filter the policy's list to their own mechanisms,
  so neither protocol library needs to change.
- The mechanisms run in `Foundation/Surl.Authentication.UnitLibrary` (`ScramSaslExchange`, ADR-0125); nothing new
  is hand-built.
- Stand-in: a test logs a Kafka or XMPP client in with `--user` alone; no store, event or fault change. `--auth`,
  when given, still replaces the whole set, as today.

## Acceptance criteria

- [x] A failing test first: a `Surl.Composition.UnitTests` test composes `--user alice:s3cret kafka://127.0.0.1:0/`
      and shows the Kafka policy's `GetSaslMechanisms` for a connection without TLS is the list the ADR states.
- [x] Tests show the same for `xmpp`, and for `kafkas`/`xmpps` with a TLS session, per the ADR.
- [x] A test shows the original servers are unchanged: `smtp`, `imap` and `pop3` with no `--auth` offer what they
      offered before (no SCRAM).
- [x] A test shows `--auth plain` turns SCRAM off on `kafka`.
- [x] If the ADR changes what `--aihelp kafka` or `--aihelp xmpp` must say, add
      `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` to `touches` before claiming and change the sentence in
      the same diff, pinned by a `Surl.Cli.UnitTests` test; otherwise no help changes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` stays at
      100% line and branch coverage.

## Notes

- If BL-1402's ADR keeps SCRAM behind `--auth`, this task reduces to the help sentence the ADR names; edit `touches`
  to `Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests` before claiming.

## Notes (run)

- Default lives in `SchemeAuthenticationPolicies.For` (kafka, xmpp; TLS twins share the plaintext scheme), skipped when `--auth` is given. Added Surl.Cli projects to touches for the two help sentences of ADR-0179 decision 6. Tests build the policy directly; the composed start passes `GivenAuthenticationMethods is not null`.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. kafka/xmpp offer SCRAM without --auth per ADR-0179; help updated
