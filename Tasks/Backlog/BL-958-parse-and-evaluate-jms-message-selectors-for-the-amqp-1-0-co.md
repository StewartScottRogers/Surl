---
id: BL-958
title: Parse and evaluate JMS message selectors for the AMQP 1.0 container
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-958 — Parse and evaluate JMS message selectors for the AMQP 1.0 container

## Goal

A hand-built JMS selector recogniser in `Surl.Extended.Protocol.Amqp1` parses a selector string once and then
answers TRUE, FALSE or NULL for any message, so the container can apply `apache.org:selector-filter:string`.

## Context

- ADR-0146 decision 2.2: the grammar (Jakarta Messaging 3.1 section 3.8.1), three-valued logic, the JMS header
  identifiers and the AMQP fields they read, the 4096-character and 32-level bounds.
- Base class library only: no parser library. Keep each method at cyclomatic complexity 10 or less
  (`CodeMetricsConfig.txt`); a precedence-climbing parser over a small tokenizer fits.
- The evaluator reads a message through a small interface (properties, header, application properties), so it
  is tested without a link and BL-959 can hand it a decoded message or a bus message.
- Matrix rows: `Amqp1:Surl-Filters-2`, `Amqp1:Surl-Filters-3` and the BL-958 part of `Amqp1:Surl-Filters-4`.

## Acceptance criteria

- [ ] Data-driven tests cover each construct: string, exact and approximate numeric and boolean literals,
      `''` in strings, `NOT`/`AND`/`OR` precedence, every comparison, arithmetic with unary minus, `BETWEEN`,
      `IN`, `LIKE` with `%`, `_` and `ESCAPE`, `IS [NOT] NULL`, each with its `NOT` form and keywords in any case.
- [ ] Tests pin three-valued logic: a missing identifier is NULL, `NULL AND FALSE` is FALSE, `NULL OR TRUE` is
      TRUE, and a NULL result does not select.
- [ ] Tests show each JMS header identifier reads the field ADR-0146 decision 2.2 names, `JMSPriority` 4 when
      the header has none, and `JMSDeliveryMode` `'PERSISTENT'`/`'NON_PERSISTENT'`.
- [ ] A selector over 4096 characters, nested deeper than 32 levels, or malformed is refused with a typed
      reason and no exception escapes; an empty selector selects everything.
- [ ] 100% line and branch coverage holds, and every method is at complexity 10 or less.

## Notes

- Filed by BL-707 (ADR-0146 decision 2.2).

## Log

- 2026-11-06: Created.
