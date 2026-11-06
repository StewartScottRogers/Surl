# Surl.Control.UnitLibrary

The loopback control endpoint (ADR-0124 decision 7; built by BL-814): HTTP and JSON under `/v1/` that
let a test suite not written in .NET drive a running `surl` with nothing but an HTTP client.
Namespace `Surl.Control`.

## What it holds

- `ControlEndpoint` - one per instance. `FeedEventsAsync` fills its own event log from the target
  (the last 10000 events and at most 64 MiB of exchange bytes, the oldest dropped) for the
  instance's life; `ServeAsync(IConnection, ExchangeContext)` answers one control connection
  through `Surl.HttpServing`'s `Http1ConnectionServer`, with the exchange's upload limit set to
  `MaxBodyBytes` (16 MiB). It keeps at most 4 snapshots, `p1`, `p2`, ... the oldest dropped.
- `IControlTarget` - what the endpoint drives: the event subscription, the fault plan (rules given
  as `--fault` item text, so there is one fault grammar) and the store control (seed, delete,
  reset, snapshot, restore). `Surl.Composition` implements it over a composed instance (BL-816).
  A member refuses with `ControlRefusalException` (`BadRequest` 400, `NotFound` 404, `Conflict`
  409), whose message is sent to the client. The target, not the endpoint, publishes each change
  on the event stream.
- `IControlSnapshot`, `ControlFault`, `ControlMessage` - the values that cross the interface.
- `ControlToken` - the bearer token, 32 to 1024 visible ASCII bytes, held only as its SHA-256 and
  compared with `CryptographicOperations.FixedTimeEquals`.
- Internal: the request handler (peer, token, routing), the routes and their requests, the event
  log, the JSON shapes (`ControlJsonContext`, `System.Text.Json` source generation, snake_case,
  no reflection), and the text-or-base64 rule for bytes (ADR-0103 decision 3).

## Rules every request follows

1. A peer that is not loopback (`127.0.0.0/8`, `::1`, either IPv4-mapped) is refused `403`, token
   or not, with no delay.
2. A request without exactly one `Authorization: Bearer <token>` field holding the token is
   refused `401` with `WWW-Authenticate: Bearer`, after `ControlEndpoint.RefusalDelay` (1 second,
   `AuthenticationPolicy.RefusalDelay`'s value) on the exchange's `TimeProvider`.
3. Then the path (`404`), the method (`405`), and the query - each key at most once, only the
   keys the request takes, numbers in range (`400`).
4. A body past 16 MiB is `413`; one whose transfer coding cannot be read or that ends early is
   `400`; a body of the wrong `Content-Type` is `415`. `401`, `403`, `413` and an unreadable body
   close the connection.

No answer holds the token or a password: JSON errors name where a body went wrong
(`$.password`), never the value there.

## References

`Foundation/Surl.Protocol.Abstractions.UnitLibrary` (the event records, `IConnection`,
`ExchangeContext`) and `Protocols/Http/Surl.HttpServing.UnitLibrary` (`IHttpRequestHandler`,
`Http1ConnectionServer`), and nothing else: no protocol server, not `Surl.Composition`, not
`Surl.Cli`. It never constructs a socket; the control listener is bound by the host through
`IListenerFactory` (BL-816).

## Tests

`Application/Surl.Control.UnitTests` drives the endpoint through `InMemoryConnection` and the real
`Http1ConnectionServer` with a hand-written `FakeControlTarget` and a manual `TimeProvider`, and
pins each answer's bytes. The generated JSON context is the one `[ExcludeFromCodeCoverage]` in
this library, with its justification.
