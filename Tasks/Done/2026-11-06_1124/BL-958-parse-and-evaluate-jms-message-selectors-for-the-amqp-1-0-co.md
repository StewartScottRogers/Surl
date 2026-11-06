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
completed: 2026-11-06
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

- [x] Data-driven tests cover each construct: string, exact and approximate numeric and boolean literals,
      `''` in strings, `NOT`/`AND`/`OR` precedence, every comparison, arithmetic with unary minus, `BETWEEN`,
      `IN`, `LIKE` with `%`, `_` and `ESCAPE`, `IS [NOT] NULL`, each with its `NOT` form and keywords in any case.
- [x] Tests pin three-valued logic: a missing identifier is NULL, `NULL AND FALSE` is FALSE, `NULL OR TRUE` is
      TRUE, and a NULL result does not select.
- [x] Tests show each JMS header identifier reads the field ADR-0146 decision 2.2 names, `JMSPriority` 4 when
      the header has none, and `JMSDeliveryMode` `'PERSISTENT'`/`'NON_PERSISTENT'`.
- [x] A selector over 4096 characters, nested deeper than 32 levels, or malformed is refused with a typed
      reason and no exception escapes; an empty selector selects everything.
- [x] 100% line and branch coverage holds, and every method is at complexity 10 or less.

## Notes

- Filed by BL-707 (ADR-0146 decision 2.2).
- Built: `Amqp1Selector.Parse` (public; returns `Amqp1SelectorParseResult` with an `Amqp1SelectorRefusal` of
  `TooLong`, `TooDeep` or `Malformed`, never an exception), `Evaluate` (TRUE/FALSE/NULL as `bool?`) and
  `Selects`; `IAmqp1SelectorMessage` (header, properties, application property by name) is the seam BL-959
  implements for a decoded or bus message. Internals: `Amqp1SelectorTokenizer`, recursive-descent
  `Amqp1SelectorParser`, node classes, `Amqp1SelectorValues`, `Amqp1SelectorIdentifiers`,
  `Amqp1SelectorLikePattern`. 230 data-driven cases in `Amqp1SelectorTests` and `Amqp1SelectorIdentifiersTests`.
- Coverage measured with `Measure-CodeQuality.ps1 -SkipTestRun` on the Amqp1 test run: the library is 100% line
  and 100% branch; no selector member is over 10 by CA1502 or by Cobertura's count (the six members the script
  still lists are older `Amqp1BusMessages`/`Amqp1ContainerConnection` code, CA1502-clean).
- Choices where the specification leaves room (sensible defaults, rule 1):
  - Nesting depth counts each parenthesised group, `NOT` and unary sign as one level; 32 is accepted, 33
    refused. Long `AND`/`OR`/arithmetic chains are bounded by the 4096 characters, not by depth.
  - Null and white-space-only selectors are empty and select everything.
  - Literals follow Java as Jakarta says: decimal, `0x` hex, leading-zero octal and `L` for exact; a point,
    an exponent or an `F`/`D` suffix for approximate. A number running into a letter or a second point is
    malformed. `-9223372036854775808` is malformed (the magnitude does not fit before the sign applies).
  - `NULL` is only valid in `IS [NOT] NULL`; `IN`, `LIKE` and `IS NULL` need an identifier on the left
    (Jakarta's grammar); `IN` lists string literals only; the escape must be exactly one character, and a
    pattern ending in it is malformed; an escape before an ordinary character makes it literal.
  - No static type check: a selector whose value is not boolean (`n`, `n + 1`) parses and is NULL, so it
    selects nothing. Logic over a non-boolean is NULL.
  - Arithmetic is `long` (wrapping, Java-like) when both sides are exact, `double` otherwise; exact division
    by zero is NULL, `long.MinValue / -1` wraps; arithmetic on a non-number is NULL. Strings and booleans
    compare only with `=`/`<>` (other operators FALSE); unlike types compare FALSE as section 3.8.1.2 says;
    NaN equals nothing and differs from everything. `IN`/`LIKE` on a non-string value is NULL, so neither
    form selects.
  - `JMSMessageID`/`JMSCorrelationID`: a string id as is; a ulong, uuid or binary id as the AMQP JMS Mapping
    draft's `ID:AMQP_ULONG:`, `ID:AMQP_UUID:` (lower-case D form), `ID:AMQP_BINARY:` (upper-case hex) text;
    any other id NULL. Application properties: integers as `long` (a ulong past `long` as `double`),
    float/double as `double`, symbol as string, timestamp as milliseconds, anything else (uuid, binary,
    char, decimal) NULL. Header identifiers match case-sensitively, like every identifier.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Amqp1Selector parses JMS selectors (Jakarta Messaging 3.1 section 3.8.1) once and evaluates them with three-valued logic over IAmqp1SelectorMessage, refusing too-long, too-deep and malformed selectors with a typed reason
