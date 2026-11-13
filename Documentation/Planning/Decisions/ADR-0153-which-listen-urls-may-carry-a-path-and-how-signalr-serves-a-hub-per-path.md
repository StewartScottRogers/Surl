# ADR-0153 — Which listen URLs may carry a path, and how SignalR serves a hub per path

- **Status:** Accepted
- **Date:** 2026-11-12
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-12, in BL-1039.
- **Amends:** [ADR-0007](ADR-0007-the-phase-1-command-line-surface.md) section 4 (a listen URL has no path) and
  [ADR-0119](ADR-0119-how-the-signalr-server-answers-signalr-and-signalrs.md) decisions 0 and 1.

## Context

ADR-0119 serves a hub at the listen URL's path, but `ListenUrlParser` refuses every path and `ListenUrl`
carries none, so BL-450 registered one hub at `/` for every `signalr` and `signalrs` listen URL.

## Decisions

1. `ListenUrl` gains `Path` (`string?`, init, as written, `null` for none or `/`).
2. `ListenUrlParser` keeps a path for `signalr` and `signalrs` only. A query or fragment is still refused for
   them, and every other scheme still refuses a path, exit 3, so ADR-0007 is loosened for no other scheme.
   A later scheme that serves at a path joins the parser's one list.
3. The registration for `signalr` and `signalrs` is a `SignalRHubRoutingProtocolServer`: on each exchange it
   reads `ExchangeContext.ListenUrl.Path`, and builds on first use, then keeps, one `SignalRProtocolServer`
   with its own `SignalRHubConnections` per hub path (`/chat` and `/chat/` are one). Two listen URLs with two
   paths are two hubs; two with one path share one.
