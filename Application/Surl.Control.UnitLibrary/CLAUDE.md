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
  as `--fault` item text, so there is one fault grammar) and the store control (seed, delete, read
  back, account changes, reset, snapshot, restore). `Surl.Composition` implements it over a composed
  instance (BL-816).
  A member refuses with `ControlRefusalException` (`BadRequest` 400, `NotFound` 404, `Conflict`
  409), whose message is sent to the client. The target, not the endpoint, publishes each change
  on the event stream.
- Seed routes for the stores outside ADR-0124's four (ADR-0158 decision 9, BL-1168): `PUT /v1/keys/{database}/{key}`
  (query `expire_ms`), `POST /v1/directory` (`text/plain` LDIF) and `PUT /v1/zones/{apex}` (`text/plain` zone file);
  `IControlTarget` gains `SeedKeyAsync`, `SeedDirectoryAsync` and `SeedZoneAsync`. No `DELETE`: reset does it.
- Read-back routes (ADR-0164 decisions 1 to 4 and 7, BL-1203), all `GET`: `/v1/mail/{owner}`, `/v1/mail/{owner}/{mailbox}`
  (`after_uid`, `limit`), `/v1/mail/{owner}/{mailbox}/{uid}` (`message/rfc822` bytes with `X-Surl-Uid` and `X-Surl-Flags`, or JSON
  for `Accept: application/json`), `/v1/files/{path}` (bytes, or a folder listing when the path ends in `/`; `after_name`,
  `limit`), `/v1/queues/{name}/messages` (`after_position`, `limit`), `/v1/topics/{name}/retained` and `/v1/topics/{name}/log` (`from_offset`, `limit`), and for the Redis keyspace (BL-1218, decision 9) `/v1/keys/{key}` (`database`) and `/v1/keys` (`pattern`, `after_key`, `limit`, `database`; keys, never values), through `ReadKeyAsync` and `ListKeysAsync`, and `/v1/directory` (`text/ldif`, `userPassword` omitted; BL-1219) through `ReadDirectoryLdifAsync`, and `/v1/zones` (`{"zones":[apex...]}`) and `/v1/zones/{apex}` (`text/dns`; BL-1220) through `ListZonesAsync` and `ReadZoneAsync`.
  `IControlTarget` gains the matching read members; an absent item is `ControlRefusalKind.NotFound`
  (404), one past 16 MiB is the new `ControlRefusalKind.TooLarge` (413 `too_large`). Reads publish nothing.
- Account routes (ADR-0170 decision 4, BL-1282): `POST /v1/proxy-accounts` and `POST /v1/tls-srp-accounts` (JSON `{"user","password"}`, as `/v1/accounts`) and `DELETE` of `/v1/proxy-accounts/{user}` and `/v1/tls-srp-accounts/{user}`, through `AddProxyAccountAsync`, `RemoveProxyAccountAsync`, `AddTlsSrpAccountAsync` and `RemoveTlsSrpAccountAsync`; `204`, `404` for an unknown user, `409` when the store is not composed. The `store` event filter takes `proxy_accounts` and `tls_srp_accounts`.
- Password changes (ADR-0132 amendment 1): `PUT /v1/accounts/{user}`, `PUT /v1/proxy-accounts/{user}` and `PUT /v1/tls-srp-accounts/{user}` (JSON `{"password"}`), through `SetAccountPasswordAsync`, `SetProxyAccountPasswordAsync` and `SetTlsSrpAccountPasswordAsync`; `204`, `404` for an unknown account, `400` for an empty password, and `409` for the proxy and TLS-SRP ones when that store is not configured.
- Bearer-token account (ADR-0132 amendment 2, BL-1602): `POST /v1/accounts` accepts `"user": ""` (the account book only; proxy and TLS-SRP adds still refuse an empty user), and `PUT` / `DELETE /v1/bearer-token` (body `{"password"}` for `PUT`) rotate and remove it through `SetBearerTokenAsync` and `RemoveBearerTokenAsync`; `204`, or `404` when there is no such account. `/v1/accounts/` with an empty segment stays `404`.
- Events are read and written through their own members (ADR-0171, BL-1285): `ControlEventMemberWriter` builds the JSON, `ControlEventKinds.Known` is listed explicitly (AOT) and pinned by a test against the exported `SurlEvent` records.
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
