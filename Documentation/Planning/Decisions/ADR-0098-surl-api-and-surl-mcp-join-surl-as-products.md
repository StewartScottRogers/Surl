# ADR-0098 — Surl.Api and Surl.Mcp join surl as Surl's products

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Stewart, 2026-11-04. Recorded by Claude in BL-610.
- **Reverses:** the answer to Product Overview open question 4 ("A managed NuGet API, or the
  `surl` executable only?"), answered "the executable" by Stewart on 2026-09-28.
- **Builds on:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) (upstream curl is
  the only oracle),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (secure by
  default, loosened only by explicit options) and
  [ADR-0079](ADR-0079-projects-are-grouped-in-category-folders.md) (category folders).

## Context

On 2026-09-28 Stewart answered Product Overview open question 4 with "the executable": `surl`
was Surl's only product, and its libraries were implementation, not a published API. On
2026-11-04 he reversed that answer. Two uses need Surl without a `surl` process on the
command line:

- a .NET test suite that wants to stand up a server in-process, configure it and stop it, as
  the Curl port's `Curl.Api` lets a test suite drive curl in-process;
- an AI agent that wants to start, list and stop Surl servers and see what a client sent, as
  the Curl port's `Curl.Mcp` lets an agent drive curl.

## Decision

1. **Surl ships two more hosts beside the `surl` executable.**
   - **`Surl.Api`** - a .NET programming interface that starts and stops Surl's protocol
     servers in-process. A .NET test suite can stand up a server without launching a process:
     for example start an FTP or HTTP server on `127.0.0.1` port 0, read the bound address,
     configure accounts, content and limits the way the command line does, and stop it.
   - **`Surl.Mcp`** - an MCP server host on standard input and output whose tools start, list
     and stop Surl servers and show what a client sent.
2. **Both live in `Application/`** beside `Surl.Console` and `Surl.Cli` (ADR-0079), and both
   are composition roots like `Surl.Console`: they compose the protocol servers, and no
   protocol server references them.
3. **Both keep the command line's secure-by-default rules** (ADR-0032). A server started
   through `Surl.Api` or `Surl.Mcp` is as closed as one started by `surl` with the same
   settings; each loosening is an explicit setting, as each is an explicit option on the
   command line.
4. **Both are modelled on the Curl port's `Curl.Api` and `Curl.Mcp` hosts.** Code may be
   copied from the port where it saves work; the port is never an oracle, a test client or a
   source of expected bytes (ADR-0003).
5. **Upstream curl stays Surl's only oracle.** Nothing about the hosts changes what a protocol
   server answers: a server answers the same bytes however it was started.
6. **The design is decided later, not here.** BL-611 decides the server composition the three
   hosts share and `Surl.Api`'s programming interface, including how it is shipped; BL-612
   decides `Surl.Mcp`'s tools and the MCP core it shares. Until those ADRs exist, neither
   project exists, and every statement about them in this repository is intent.

## Consequences

- The Product Overview's "A managed NuGet API" non-goal is withdrawn, its open question 4 reads
  "Reversed 2026-11-04", and its "Users" adds .NET test suites and AI agents.
- Root `CLAUDE.md` names `Surl.Api` and `Surl.Mcp` as planned hosts in `Application/`, and as
  naming exceptions beside `Surl.Console`.
- Each host is a production project, so it is native-AOT compatible and held to the solution's
  quality gates like `Surl.Console`.
