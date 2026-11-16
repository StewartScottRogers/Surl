# ADR-0171 — How a new SurlEvent kind reaches Surl.Api, Surl.Mcp and the control endpoint

- **Status:** Accepted
- **Date:** 2026-11-16
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-16, in BL-1245.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 1, 2, 7 and 8.
- **Amends:** ADR-0124 decision 2 ("Extension points").
- **Acted on by:** BL-1283, BL-1284, BL-1285, BL-1286.

## Context

Each host switches on an event's type: `Surl.Api` (`SurlEventFacts`, `SurlEventKind`), the control endpoint
(`ControlEventKinds`, `ControlEventWriter`) and `Surl.Mcp` (`EventToolFilter`, `RecordedEventWriter`). An unknown
type falls through silently (`null`, `head`), so a new record is neither found by kind nor written in full, and no
test says so. `ConsumerGroupChangedEvent` needed every one edited by hand. This breaks "never into a central switch
that must be edited" (root `CLAUDE.md`).

## Decision

**The event says what hosts need; hosts read it.** `SurlEvent` (in `Surl.Protocol.Abstractions`) gains:

- `virtual long? ExchangeId`, `virtual string? Scheme` and `virtual string? Account`, each `null` on the base and
  overridden by a record that has the fact (a store change's account is its name when the store is accounts);
- `string KindName`, derived from the type name without `Event`, in snake_case;
- `virtual void WriteMembers(ISurlEventMemberWriter writer)`, where the writer has `Text`, `Number`, `Flag`,
  `Choice(name, Enum)` and `Bytes(name, ReadOnlyMemory<byte>)`. Each record writes its own members; each host
  supplies a writer that builds its JSON (names in snake_case, bytes bounded as today).

No reflection at run time, so it is AOT-safe, and a new record is complete in one place.

**What stays by hand, and the test that fails until it is done.** `Surl.Api`'s public `SurlEventKind` enum keeps
one member per kind (a .NET test needs a typed value); `KindOf` maps `KindName` with `Enum.TryParse<SurlEventKind>`.
The `Surl.Mcp` `kinds` enum in the input schema is derived from the exported event types, not hand-written.

**Guards.** Every host's tests enumerate `typeof(SurlEvent).Assembly`'s exported subclasses (as
`SurlEventContractTests.EventRecordTypes` does) and fail when one is not queryable by kind, not filterable, or has a
public property missing from the host's written output. `Surl.Protocol.Abstractions.UnitTests` fails when a record
writes none of its properties or shadows `ExchangeId`, `Scheme` or `Account` without overriding them.

### Rejected

- **Source-generated `System.Text.Json` for the members.** AOT-safe, but each type is listed in a context class (or a
  polymorphism attribute) in every host: the same central edit, and snake_case enum and byte rules need converters.
- **Keep the switches and add completeness tests only.** Tests would catch the miss, but every new kind still costs
  an edit in six places; the base members remove the edit, not just the silence.

## Amendment to ADR-0124 decision 2

Add to "Extension points": **Event kinds** - a new `SurlEvent` record overrides `ExchangeId`, `Scheme`, `Account` and
`WriteMembers` where it has them; `Surl.Api` gains one `SurlEventKind` member; the control endpoint and `Surl.Mcp`
need no edit; a guard test in each host fails until all this is true.

## Consequences

BL-1283 adds the contract and its guard; BL-1284 (`Surl.Api`), BL-1285 (control endpoint) and BL-1286 (`Surl.Mcp`)
then drop their switches. No wire or JSON byte changes.
