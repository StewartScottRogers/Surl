---
id: BL-1411
title: Offer PLAIN and AMQPLAIN over amqp:// under --allow-anonymous, as ADR-0112 decision 2 says
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1411 — Offer PLAIN and AMQPLAIN over amqp:// under --allow-anonymous, as ADR-0112 decision 2 says

## Goal

Under `--allow-anonymous`, the AMQP 0-9-1 broker's `connection.start` `mechanisms` lists `PLAIN` and `AMQPLAIN` on
`amqp://` as well as `amqps://` (credentials not checked), so a 0-9-1 client that only speaks `PLAIN` gets in, as
ADR-0112 decision 2 says.

## Context

- Found while checking finding 5 of the Surly cookbook bug report 2026-11-19 09:30 (the `amqp` help): the cookbook
  recipe `Z:\repos\Surly\Book\parts\05-new-protocols\chapters\02-messaging\recipes\amqp-send-and-receive`
  (`recipe.yml` case `anonymous-is-not-enough`; `review.md` finding 1) shows `surl --allow-anonymous amqp://...`
  offering `ANONYMOUS` and no `PLAIN`, so a `PLAIN`-only client is refused (the client was the Curl port, which
  located the gap; ADR-0112 is the oracle).
- ADR-0112 decision 2 (Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md,
  lines 109-116): "**Under `--allow-anonymous`, `PLAIN` and `AMQPLAIN` are offered on every transport and their
  credentials are not checked**: 0-9-1 clients log in with `PLAIN` and almost none speaks `ANONYMOUS` ..., so an
  anonymous broker that withheld `PLAIN` would refuse the clients it was loosened for." Line 498 rejects the
  alternative explicitly.
- Cause: `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0BrokerConnection.Login.cs:30-50`
  (`OfferedMechanisms`) lists only what `policy.GetSaslMechanisms` returns, then `ANONYMOUS` when
  `options.OffersAnonymous`; the policy withholds `PLAIN` without TLS unless `--allow-plaintext-auth`
  (`Foundation/Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs:713`, `OffersPlaintextSecrets` ignores
  `AllowAnonymous`). `policy.StartSaslExchange` already runs `PLAIN` under `--allow-anonymous` on any transport
  (`AuthenticationPolicy.cs:408`), so only the offer is missing.
- The work: when `OffersAnonymous` is set and the policy's list lacks `PLAIN`, add `PLAIN` then `AMQPLAIN` (after the
  policy's mechanisms, before `ANONYMOUS`). `Amqp0BrokerOptions.OffersAnonymous` is composed from `--allow-anonymous`
  (`Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs:112`), so no composition change.
- Existing pin: `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests/Amqp0BrokerTests.cs:97-104`
  (`ServeAsync_OffersAnonymous_OffersItLastAndAcceptsItAtOnce`, expects `SCRAM-SHA-256 ANONYMOUS` from a fake
  policy) changes with it.
- Stand-in: a test can point any 0-9-1 client at `surl --allow-anonymous amqp://...` with no certificate, the
  shape the MQTT, NATS and STOMP stand-ins already allow; the `--allow-anonymous` warning stands.

## Acceptance criteria

- [x] A failing test first: an `Amqp0BrokerTests` test with `OffersAnonymous = true` and a policy offering no `PLAIN`
      on a connection without TLS sees `connection.start` `mechanisms` end with `PLAIN AMQPLAIN ANONYMOUS`.
- [x] A test replays a `start-ok` with `PLAIN` `\0guest\0guest` on that connection and the connection opens
      (`tune`, `open-ok`).
- [x] A test shows that without `OffersAnonymous` the list is the policy's alone (no `PLAIN` added).
- [x] The test at `Amqp0BrokerTests.cs:97` is updated to the new list.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Extended.Protocol.Amqp0.UnitLibrary`
      stays at 100% line and branch coverage.

## Notes

- Choice: the second test replays `guest`/`guest` against a fake policy (`AcceptsAnyPlain`) standing for the real policy under `--allow-anonymous`. A policy that already lists `ANONYMOUS` now gets `ANONYMOUS PLAIN AMQPLAIN` (PLAIN added when absent, ANONYMOUS not repeated).
- BL-1407 states the result in `surl --aihelp amqp` after this lands. The cookbook's `anonymous-is-not-enough`
  case will change outcome; tell the cookbook's author in the shift report.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. amqp:// under --allow-anonymous now offers PLAIN and AMQPLAIN before ANONYMOUS
