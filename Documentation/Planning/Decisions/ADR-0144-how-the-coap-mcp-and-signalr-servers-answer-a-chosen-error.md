# ADR-0144 — How the CoAP, MCP and SignalR servers answer a chosen error

- **Status:** Accepted
- **Date:** 2026-11-06
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in BL-835.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4 and 9 (the fault model, the
  `error` kind and the extended reply table), [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md) (extended servers are
  judged by their specifications), [ADR-0097](ADR-0097-how-the-coap-server-answers-coap-and-coaps.md) (CoAP),
  [ADR-0111](ADR-0111-how-the-mcp-server-answers-mcp-mcps-and-mcp-stdio.md) (MCP),
  [ADR-0119](ADR-0119-how-the-signalr-server-answers-signalr-and-signalrs.md) (SignalR), and
  [ADR-0138](ADR-0138-how-the-dict-gopher-and-telnet-servers-answer-a-chosen-error.md) (whose pattern this one
  follows).
- **Acted on by:** BL-835.

## Context

ADR-0124's extended reply table gives `coap` and `coaps` "a response code 4.00 to 5.05", `mcp` and `mcps` "a
JSON-RPC error code and message" and `signalr` and `signalrs` "text, sent as the invocation's completion
`error`", with `command` the CoAP method, the JSON-RPC method or tool name, or the hub method, and `path` the URI
path (CoAP) or the listen path. It does not say exactly how each reply is written, where each server asks the
plan, or what a request that cannot carry an error does. None of these schemes is spoken by upstream curl, so
each answer is judged by its own specification (ADR-0083) and there is nothing to measure.

## Decision

1. **The reply shapes**, checked by `Surl.Cli`'s `FaultReplyShapes` when `--fault` is read and again by each
   server, which notes and skips a reply that does not fit (a `Surl.Api` hook can hand over anything):
   - `coap`, `coaps`: `c.dd` with class 4 and detail 00 to 31, or class 5 and detail 00 to 05 (RFC 7252
     section 3 writes codes so, and 4.00 to 5.05 is every client and server error code a 5-bit detail can
     write up to 5.05), optionally followed by a space and a diagnostic text, sent as the response's UTF-8
     payload (RFC 7252 section 5.5.2). `4.04` and `5.03 try later` fit; `2.05`, `5.06`, `4.32` and `404` do
     not.
   - `mcp`, `mcps`: a whole number with an optional sign, a space and a non-empty message: the JSON-RPC 2.0
     `error` object's `code` and `message` (section 5.1). JSON-RPC allows any integer, so no range is
     imposed; `-32603 internal error` and `7 x` fit.
   - `signalr`, `signalrs`: any non-empty text, sent as the Completion's `error` string (HubProtocol.md).
2. **CoAP** (`CoapFlowServing`, `CoapRequestFaults`). The plan is asked for every request the message layer
   hands to answering - a CON or NON request that is not a duplicate - before the resource answerer and the
   observers, with the listen URL's scheme, the method as `command` (`GET`, `POST`, `PUT`, `DELETE`; any
   other method code as `0.dd`) and the Uri-Path as `path` (`/a/b`, `/` for none). CoAP has no account (a
   `coaps` PSK identity authenticates the peer but names no account), so the point names none. The chosen
   response goes out the way the request asks for its answer - piggybacked in the ACK, or as a NON - with the
   request's token, no options and the diagnostic payload; it registers no observation and writes nothing.
   A NON with an unrecognised critical option is still ignored silently before the plan is asked.
3. **MCP** (`McpHttpRequestAnswerer`, `McpRequestFaults`). The plan is asked for every JSON-RPC request inside
   a live session, after the login, the Origin, media type, body and session checks have passed and before
   the core answers, with the listen URL's scheme, the login as `account` (none for the anonymous caller), the
   endpoint's path (`/mcp`) as `path`, and as `command` the tool's `name` for `tools/call` (when it is a
   string) and the method for every other request (`ping`, `tools/list`). The tool's name is the more useful
   key for a test - "make `read_file` fail" - and a test that wants every call to fail names `tools/call`
   via a rule without `command` or scoped by path. `initialize` is not offered to the plan: a session must
   exist before a request can fail inside it, and refusing a session is `refuse-login`'s job.
   Notifications and responses get no answer, so they are never offered. The error response is written by
   the shared core's `JsonRpcResponseWriter.WriteError` with the request's `id`, in the media type the
   request is answered in (`text/event-stream` for `tools/call`, `application/json` otherwise).
4. **SignalR** (`SignalRHubSession`, `SignalRInvocationFaults`). The plan is asked for every Invocation or
   StreamInvocation that has an `invocationId`, after the duplicate-ID check and before the hub method, with
   the listen URL's scheme, the account the connection was negotiated by (none for the anonymous caller), the
   hub's endpoint base (`/chat`) as `path` and the hub method (`target`, as sent) as `command`. A chosen
   error is the Completion `{"type":3,"invocationId":"<id>","error":"<reply>"}`; a StreamInvocation so
   answered gets no StreamItem. A non-blocking Invocation has no Completion to carry an error, so the plan is
   not asked for one and it runs as usual.
5. **Common rules**, as ADR-0138 decisions 4 and 5: each application publishes one `FaultAppliedEvent` (stage
   `Command`, the point's path as its target) and notes the rule's ID; a rule of another kind at these
   points, or a reply that does not fit, is noted (`Fault rule <id> not applied to the request: ...`) and the
   request is served as without it. The listen URL is matched as `ReportedListenUrl.Format` names it.

## Consequences

- A test can make a CoAP resource answer `5.03`, an MCP tool return a JSON-RPC error, or a SignalR hub method
  fail, alone or beside any other protocol, without a custom server.
- With no rule every existing exchange is unchanged; the plan is asked once per request, which the default
  empty plan answers at once.
- Command-stage `delay` and `drop` for these servers are not applied here; they follow ADR-0141's
  `CommandStageFaults` in a later task.
