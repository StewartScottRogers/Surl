---
id: BL-235
title: Record in ADR-0049 how EXTERNAL answers without a client certificate and how help wraps a long default
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-216]
touches: [Documentation/Planning/Decisions]
requirement: FR-046
created: 2026-09-29
completed: 2026-09-30
---
# BL-235 — Record in ADR-0049 how EXTERNAL answers without a client certificate and how help wraps a long default

## Goal

ADR-0049 records, as an amendment marked "Decided by Claude under Stewart's delegation", the two
choices BL-216 made while building SASL `EXTERNAL`, so the ADR says what the code does.

## Context

- BL-216 could not edit `Documentation/Planning/Decisions` because BL-155 held it in `Doing`;
  its `Notes` state both choices and why.
- Choice 1: `AuthenticationPolicy.StartSaslExchange` answers `EXTERNAL` on a connection with no
  TLS client certificate as `RefusedMechanism`, undelayed and unnoted, even under
  `--allow-anonymous` (`AuthenticationPolicy.CanIdentifyClient`), because the mechanism is not
  offered there and has no client identity to accept.
- Choice 2: `HelpLayout.WrapParagraph` breaks a word too long for any line after each of its
  commas, so `--auth`'s default (`digest,...,external,aws-sigv4`, 79 characters) wraps within
  79 columns (ADR-0034's width).
- `Surl.Authentication.UnitTests/ExternalSaslMechanismTests.cs`
  (`NoClientCertificate_IsRefusedAsAMechanismUndelayedAndNotOffered`) and
  `Surl.Cli.UnitTests/HelpTextTests.cs` (`Answer_Auth_IsItsPageWithItsDefaultWrappedAndItsExplanation`)
  pin them.

## Acceptance criteria

- [x] ADR-0049 has an amendment section, marked "Decided by Claude under Stewart's delegation",
      stating choice 1 and why.
- [x] ADR-0034 (or ADR-0049, naming ADR-0034) states choice 2.
- [x] ADR-0049's statements that `external` is "refused as not available until BL-216" read as
      history, with BL-216 named as done.

## Notes

- Did it in session (a docs task of three ADR edits; no `.cs` changed, so the `verify` skill was not needed; build clean and fast tests green anyway). Choice 1 and choice 2 are ADR-0049 amendment 1, checked against `AuthenticationPolicy.CanIdentifyClient` and `HelpLayout.WrapParagraph`; ADR-0034 decision 3 now states the comma-breaking wrap and links the amendment; ADR-0049 decisions 3, 4 and 8 and the Decisions README row read `external` as built by BL-216 (done), with `gssapi` still refused until BL-218.

## Log

- 2026-09-29: Created.
- 2026-09-29: Filed by BL-216.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0049 amendment 1 records EXTERNAL's undelayed RefusedMechanism without a client certificate and help's comma-breaking wrap of --auth's default; ADR-0034 decision 3 states the wrap
