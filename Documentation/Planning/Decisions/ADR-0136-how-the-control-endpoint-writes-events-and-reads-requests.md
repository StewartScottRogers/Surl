# ADR-0136 — How the control endpoint writes events and reads requests

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-814.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 7 (the loopback control endpoint)
  and decision 1 (the event stream), [ADR-0103](ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md) decision 3 (`get_exchanges`'s shape
  and the text-or-base64 rule).
- **Acted on by:** BL-814 (`Application/Surl.Control.UnitLibrary`).

## Context

ADR-0124 decision 7 fixes the control endpoint's paths, bodies, answers, error codes and bounds. Building
`Surl.Control` left smaller questions it does not answer: how each event record's members are named where they
clash with the event's own `kind`, what the query filters match on events that carry no scheme, what the
dropped counts and `is_truncated` count when the endpoint holds its own subscription, the order of the peer and
token checks, and how strictly a query or a JSON body is read. Upstream curl is not consulted: the control
endpoint is Surl's own interface for test suites, not a protocol curl speaks, so no byte here is curl's to
judge (ADR-0124 decision 7 made the same call).

## Decision

1. **Event JSON.** `sequence`, `at` (UTC, round-trip format) and `kind` (the record's type name without
   `Event`, in snake_case: `exchange_started`, `exchange_bytes`, `exchange_ended`, `login_judged`,
   `store_changed`, `fault_applied`, `hook_failed`), then the record's members in snake_case, enum values in
   snake_case. A null member is left out. `FaultAppliedEvent.Kind` is written `fault_kind`, because `kind` is
   the event's. An `exchange_bytes` event writes `direction`, `encoding`, `data`, `byte_count` (all its bytes)
   and `is_cut` (whether more than 65536 were held back).
2. **Query filters.** `scheme` matches an exchange start's or a judged login's scheme, or the scheme of the
   exchange another event belongs to (by its exchange ID, while that exchange's start is held), case-blind;
   `listen_url` matches the exchange's listen URL exactly; `account` matches a judged login's account or the
   name an account-store change touched; an event with nothing to match is left out by that filter.
3. **Counts.** `dropped_event_count` counts every event the endpoint lost: dropped from its log past 10000
   events or 64 MiB of bytes, or skipped by its subscription (a gap in `sequence`). `dropped_exchange_count`
   counts exchange starts dropped from the log; an exchange is listed only while its start is held.
   `is_truncated` is true when the subscription skipped events between an exchange's start and its end (or
   now, if it has not ended), so some of its bytes may be missing.
4. **Waiting.** `GET /v1/events/wait` answers the first held event the query matches, or the next one added;
   a client that wants only new events gives `after`. Its bound is the injected `TimeProvider`.
5. **Order of refusals.** The peer is checked first: a non-loopback peer (an IPv4-mapped loopback address
   counts as loopback; an end point that is not an IP address does not) is `403` at once, token or not. Then the
   token: `401` after the fixed one-second delay. Then path (`404`), method (`405`, naming the methods the path
   takes), query, media type, body. `401`, `403`, `413` and a body that cannot be read close the connection;
   every other answer keeps it.
6. **The token.** Exactly one `Authorization` field, scheme `Bearer` in any case, the token after it with
   surrounding blanks trimmed. Both sides are hashed with SHA-256 before `FixedTimeEquals`, so the comparison
   takes the same time whatever the presented token's length.
7. **Strict reading.** A query key given twice, or one the request does not take, is `400` naming the key. A
   JSON body member the request does not take is `400`; a JSON error names the path where the body went wrong
   (`$.queue`), never the value, so a password is never echoed. In `POST /v1/messages`, exactly one of a
   non-empty `topic` and `queue`, exactly one of `payload_text` (sent as UTF-8) and `payload_base64`, and
   `retain` only with a topic. In `POST /v1/accounts`, `user` and `password` are both required and non-empty.
   A fault item's trailing CR and LF are dropped; an empty item is `400`.
8. **Paths.** `{path}` and `{user}` are percent-decoded segment by segment; a file path is passed to the
   target with a leading `/`. `/v1/files/` with nothing after it is `404`.
9. **Answer fields.** Every answer has `Date` and `Server: surl`; a JSON answer adds `Content-Type:
   application/json`, `Cache-Control: no-store` and `Content-Length`; a `204` has no `Content-Length` (RFC 9110
   section 8.6).
10. **Coverage.** The generated `ControlJsonContext` is the library's one `[ExcludeFromCodeCoverage]`: its body
    is the `System.Text.Json` source generator's, and the shapes it writes are pinned byte for byte by the
    endpoint's tests.

## Alternatives considered

- **`kind` for the fault's kind, the event kind under another name.** Rejected: `kind` names the event in every
  other answer and in the `kind` query key.
- **Token before peer.** Rejected: a non-loopback peer is never allowed, so it learns nothing from a delay, and
  checking it first keeps the token's comparison off every request from outside.
- **Lenient queries** (ignore unknown keys). Rejected: a misspelt `listen_url` would silently match everything,
  which a test suite would read as a pass.

## Consequences

- `Surl.Composition`'s `IControlTarget` (BL-816) must throw `ControlRefusalException` with a message that holds
  no secret, and publishes each change itself; the endpoint publishes nothing.
- A later event record joins the endpoint with its type name as its kind and its members by name, once
  `ControlEventWriter` maps them.
