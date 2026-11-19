---
id: BL-1402
title: Decide whether kafka and xmpp offer SCRAM without --auth naming it, and amend ADR-0105, ADR-0114 and ADR-0125 to agree
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-19
completed:
---
# BL-1402 — Decide whether kafka and xmpp offer SCRAM without --auth naming it, and amend ADR-0105, ADR-0114 and ADR-0125 to agree

## Goal

An ADR, "Decided by Claude under Stewart's delegation", settles whether the Kafka broker and the XMPP server offer
SCRAM (`SCRAM-SHA-256`, `SCRAM-SHA-512` for Kafka; `SCRAM-SHA-256`, `SCRAM-SHA-1` for XMPP) when `--auth` is not
given, and amends ADR-0105 decision 5, ADR-0114 decision 3 and ADR-0125 decision 9 so the three agree.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 4: `surl --user alice:s3cret kafka://127.0.0.1:9092/` answers
  SaslHandshake with error 33 and an empty mechanism list, though `surl --aihelp kafka`
  (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:295`) says `PLAIN`, `SCRAM-SHA-256` and `SCRAM-SHA-512` log in.
  Finding 2's log shows the same on XMPP: only `PLAIN`, before and after STARTTLS.
- The recorded decisions disagree, which is why this is a decision and not a fix:
  - ADR-0105 decision 5: "The policy already withholds `PLAIN` on a connection without TLS unless
    `--allow-plaintext-auth` (FR-028) is given, so `kafka://` offers SCRAM alone by default and `kafkas://` all
    three."
  - ADR-0114 decision 3: the mechanisms offered are `SCRAM-SHA-256`, `SCRAM-SHA-1`, `PLAIN`, then `ANONYMOUS`; its
    Consequences say `surl xmpp://localhost:0/ --self-signed --user alice:secret` "logs `alice@localhost` in with
    SCRAM or PLAIN".
  - ADR-0125 decision 9 (and its Amendment 1 for SCRAM-SHA-512): SCRAM is not in
    `AuthenticationMethods.DefaultAccepted`; "a server offers it only when `--auth` names it". Its stated reason:
    "no original server's offer, and so no byte upstream curl sees, changes unless `--auth` asks for them".
- The code follows ADR-0125: `Foundation/Surl.Authentication.UnitLibrary/AuthenticationMethods.cs:17-29`
  (`DefaultAccepted` holds no SCRAM), `AuthenticationPolicy.cs:169` keeps only accepted mechanisms, and
  `Application/Surl.Composition.UnitLibrary/AuthenticationComposition.cs:174` uses `DefaultAccepted` when `--auth`
  is not given. `KafkaSaslHandshakeAnswerer.Offered`
  (`Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary/KafkaSaslHandshakeAnswerer.cs:56`) therefore keeps
  nothing over `kafka://`. Today's workaround is `--auth scram-sha-256,scram-sha-512,plain`.
- What to weigh, by the standing rules (root `CLAUDE.md`, "Decisions"; ADR-0032; ADR-0083):
  - ADR-0125's reason does not reach the extended schemes: upstream curl speaks neither `kafka` nor `xmpp`, so a
    default for those schemes changes no byte upstream curl sees.
  - SCRAM never sends the password (RFC 5802), so ADR-0032's plain-text rule does not withhold it. The mail
    servers' precedent is `CRAM-MD5`: in `DefaultAccepted`, and offered over plain `smtp://`, `imap://` and
    `pop3://`.
  - Under the ADR-0125 reading, `kafka://` with an account cannot be logged in to at all without `--auth` or
    `--allow-plaintext-auth`: a Kafka broker implements no other mechanism Surl offers.
  - Where the default would live: `DefaultAccepted` for every server (which changes the original mail servers'
    offer, against ADR-0125), or a per-scheme default in the composition (`AuthenticationPolicyFor(scheme)`,
    `Application/Surl.Composition.UnitLibrary/ComposedProtocolServices.cs:110`) for `kafka`/`kafkas` and
    `xmpp`/`xmpps` only, only when `--auth` is not given.
  - Configuration only grows, and defaults keep today's behaviour (root `CLAUDE.md`, "Surl is a stand-in"): say
    whether turning SCRAM on for two extended schemes is a change a test relying on today's default could see.
- Stand-in: the outcome decides whether a test can log a Kafka or XMPP client in over plain text with only
  `--user`, the shape the other extended brokers' stand-ins use.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` (the next free number, listed in that folder's
      `README.md`), marked "Decided by Claude under Stewart's delegation", states which mechanisms `kafka`, `kafkas`,
      `xmpp` and `xmpps` offer with an account and no `--auth`, on each TLS state, and where in the code the default
      lives.
- [x] It weighs the points in Context, the `CRAM-MD5` precedent included, and records the rejected alternative.
- [x] ADR-0105 decision 5, ADR-0114 decision 3 and ADR-0125 decision 9 each gain an amendment note pointing to it, so
      no two of them contradict.
- [x] The ADR gives the exact sentence `surl --aihelp kafka` and `surl --aihelp xmpp` should state about mechanisms,
      for BL-1403 to apply.
- [x] No `.cs` file changes.

## Notes

- Decision: ADR-0179. SCRAM is offered by default on kafka/kafkas/xmpp/xmpps through a per-scheme default in the composition (not DefaultAccepted). BL-1403 implements it (code plus the two help sentences in ADR-0179 decision 6).

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
