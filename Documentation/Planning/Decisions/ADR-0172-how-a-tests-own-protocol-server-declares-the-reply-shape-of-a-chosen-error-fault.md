# ADR-0172 — How a test's own protocol server declares the reply shape of a chosen-error fault

- **Status:** Accepted
- **Date:** 2026-11-16
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-16, in BL-1246.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 2 and 4.
- **Amends:** ADR-0124 decision 2 ("Extension points").
- **Acted on by:** the task filed from BL-1246 (see its Notes).

## Context

A `SurlProtocolServer(Scheme, Create)` a test registers through `SurlServerOptions.ProtocolServers`
(BL-813) is served beside the built-in servers. `delay`, `drop`, `refuse-login` and `out-of-space`
rules already reach it. An `error` rule scoped to its scheme or listener is refused by
`CustomProtocolServers.ToReadable`, because ADR-0124 decision 4 checks an `error` rule's `reply`
against its scheme's shape, and `FaultReplyShapes.FitsByScheme` in `Surl.Cli.UnitLibrary` has no row
for a scheme Surl does not know. A test that stands in for its own protocol therefore cannot make it
answer a chosen error, though it is the one party that knows what an error looks like there.

## Decision

1. **The refusal stays the default.** A custom server that declares nothing still has an `error` rule
   scoped to it refused, as today. Nothing widens unless the test asks (secure by default).
2. **A server may declare its reply shape.** `SurlProtocolServer` gains an optional init property
   `FitsErrorReply`, a `Func<string, bool>?` that is `null` by default (additive: existing
   registrations and defaults behave as before). It plays for the custom scheme the part a
   `FaultReplyShapes` row plays for a built-in one: it says whether a rule's `reply` is a chosen
   error the protocol allows. It is called with the rule's `reply`, which is never null for an
   `error` rule; a predicate that throws is an `ArgumentException` naming the rule, not a crash.
3. **`Surl.Api` judges the reply; `surl`'s reader is not widened.** `CustomProtocolServers` checks the
   rule's `reply` with the predicate, then reads the rule with `surl`'s reader using the existing
   stand-in scheme and a reply that fits the stand-in, and puts the real `reply` and scope back.
   The shape table in `Surl.Cli.UnitLibrary` is not changed, so the command line, the `-K` file,
   `Surl.Mcp` and the control endpoint stay as strict as they are. This applies to a rule in
   `Faults` at start and to `server.Faults.Add` while serving.
4. **The server receives the rule through `ExchangeContext.Faults`**, as a built-in server does: the
   plan hands back the `error` rule at its stage (`FaultStage.Command`) and the server answers
   `Reply` in its own wire format. Surl does not format the reply for it.
5. **A response override.** An `AddOverride` answer of kind `error` for a custom scheme is judged by
   the same predicate when the exchange's scheme is custom; with no predicate, or a reply that does
   not fit, it is treated as the override's other refused answers are (the exchange proceeds
   unfaulted and a `HookFailedEvent` says why). The task checks this against the built-in path and
   aligns the two.
6. **Hosts that cannot register a scheme do not change.** The control endpoint and `Surl.Mcp` start
   instances without a test's own servers, so a custom scheme is unknown to them and an `error`
   rule naming one is refused by `surl`'s reader as an unknown scheme. No work is filed for them.
7. **Documentation moves with it.** `Application/Surl.Api/README.md` ("Your own protocol server")
   and `Application/Surl.Api/CLAUDE.md` ("A test's own protocol servers") stop saying an `error`
   rule is always refused and say it is refused unless the server declares `FitsErrorReply`.

## Consequences

- A test's own protocol can be fault-tested with chosen errors, alone or beside built-in protocols,
  with no edit to a central table (open for extension).
- One optional, defaulted property; no existing option or field changes meaning.
- A wrong predicate is the test's own bug and is reported at start or at `Add`, not mid-exchange.
