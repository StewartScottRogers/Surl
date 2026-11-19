# ADR-0181 — An accept-stage fault rule's `times` counts every connection, a port check included

- **Status:** Accepted
- **Date:** 2026-11-19
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-19, in
  BL-1395. No package, no threshold change, no download.
- **Clarifies:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4, the accept stage.

## Context

Surly cookbook bug report 2026-11-19, finding 3: a rule at stage `accept` with `times=N` was used up by a TCP
connection that opened and closed without a request, such as a readiness probe or a load-balancer health check,
so the test's first real request was served untouched.

The accept stage acts before any byte, the TLS handshake included. A `drop` there cannot wait for a byte, and a
`delay` there holds the connection before anything is read.

## Options

- (a) Keep the count: every connection that reaches the stage spends a time. Document it.
- (b) Count a rule only when the connection then sends a byte or completes a handshake. A `drop` could then no
  longer act before any byte, and `delay` could no longer hold a connection that says nothing: the stage would
  mean something else.
- (c) A new rule field choosing (a) or (b). Configuration only grows and defaults keep today's behaviour, so the
  default stays (a) and the field buys nothing now.

## Decision

1. **Option (a).** A rule at `accept` spends one of its `times` on every connection it matches, whether or not the
   connection ever sends a byte. The engine is unchanged.
2. A test that waits for surl's port and expects the fault on its first real request adds the rule after the wait:
   `Surl.Api`, the control endpoint (`POST /v1/faults`) and `surl-mcp` all add a rule to a running server. A
   rule that must exist from the start is scoped (`listener=`, `scheme=`) or given a `times` that covers the
   probes.
3. A rule at the `command` or `login` stage is counted after a byte has been read, so a port check never spends it.
4. `--aihelp testing` states the rule in one sentence, and a fast test in `Surl.Core.UnitTests` pins a connect and
   close followed by a real request against an accept-stage `times=1` rule.
5. Option (c) stays open: a field is added only when a test needs the probe not to count, with (a) its default.

## Consequences

- Determinism is kept: the same connections give the same answer every run; the probe is a connection.
- Nothing about the accept stage's meaning changes, so no existing rule, test or `--fault` item changes.
