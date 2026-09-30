---
id: BL-231
title: Record in ADR-0049 how EXTERNAL answers without a client certificate and how help wraps a long default
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-216]
touches: [Documentation/Planning/Decisions]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-231 — Record in ADR-0049 how EXTERNAL answers without a client certificate and how help wraps a long default

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

- [ ] ADR-0049 has an amendment section, marked "Decided by Claude under Stewart's delegation",
      stating choice 1 and why.
- [ ] ADR-0034 (or ADR-0049, naming ADR-0034) states choice 2.
- [ ] ADR-0049's statements that `external` is "refused as not available until BL-216" read as
      history, with BL-216 named as done.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Filed by BL-216.
