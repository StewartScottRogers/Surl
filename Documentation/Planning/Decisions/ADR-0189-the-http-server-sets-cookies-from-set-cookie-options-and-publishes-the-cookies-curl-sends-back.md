# ADR-0189 — The HTTP server sets cookies from `--set-cookie` and publishes the cookies curl sends back

- **Status:** Accepted
- **Date:** 2026-11-24
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-24, in BL-1650.
- **Builds on:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md),
  [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 1, 4 and 6,
  [ADR-0171](ADR-0171-how-a-new-surlevent-kind-reaches-surl-api-surl-mcp-and-the-control-endpoint.md).
- **Acted on by:** BL-1659 (library), BL-1660 (HTTP server), BL-1661 (options and `--aihelp`), BL-1662 (pinned curl proof).
- **Amends:** nothing. ADR-0124 decision 6 still holds (decision 6 below).

## Context

`Foundation/Surl.Cookies.UnitLibrary` is empty, yet FR-015 and the product overview say the HTTP server sets cookies
through it. The alignment and coverage audits of 2026-11-24 decided to build it. Upstream curl's `-b`/`--cookie`,
`-c`/`--cookie-jar` and `-j`/`--junk-session-cookies` need a server that sends `Set-Cookie` and lets a test see what came
back. This ADR fixes the surface so the four tasks need no question. Only upstream curl 8.21.0 is evidence (ADR-0003).

## Measurement (curl 8.21.0, `Record-CurlExchange.ps1`, 2026-11-24)

Reply to `curl -sS -c <jar> http://127.0.0.1:<port>/`, two header lines, Latin-1 bytes exactly:

```
Set-Cookie: sid=abc123; Domain=127.0.0.1; Path=/; Expires=Wed, 09 Jun 2027 10:18:14 GMT; Max-Age=3600; Secure; HttpOnly; SameSite=Lax
Set-Cookie: sess=xyz; Path=/
```

curl exited 0 and wrote this jar (tabs; fields: domain, include-subdomains, path, secure, expiry, name, value):

```
127.0.0.1	FALSE	/	FALSE	0	sess	xyz
#HttpOnly_127.0.0.1	FALSE	/	TRUE	1795552533	sid	abc123
```

- `Max-Age` wins over `Expires`: the jar expiry is the receive time plus 3600, not the `Expires` instant.
- `HttpOnly` is the `#HttpOnly_` line prefix; `Secure` is the fourth field; a `Domain` that is an IP address is kept
  with include-subdomains `FALSE`; `SameSite` is read and not stored in the jar.
- `Secure` over plain `http://127.0.0.1` is accepted and stored.
- Replay: `curl -b <jar> http://127.0.0.1:<port>/x` sent `Cookie: sess=xyz; sid=abc123` (jar order, `; ` separator, the
  secure cookie included because the host is loopback), after `Accept: */*` and as the last header.

BL-1662 repeats these as `Integration` tests against the pinned build on every platform; BL-1660's unit tests replay the
bytes above. Nothing here comes from the Curl port.

## Decision

### 1. One repeatable option, `--set-cookie <spec>`, off until used

`--set-cookie 'name=value[;Attribute[=value]]...[;on-path=/prefix][;on-listener=<listen URL>]'`, repeatable, at most 64
(past it exit 2, as `--fault`'s 256). A `-K` file line `set-cookie = "..."`, `Surl.Api`'s `SurlServerOptions.Cookies` (amendment 1) and `Surl.Mcp`'s
`start_server` argument `cookies` carry the same list ("configuration
as data"); the control endpoint starts no server, so it has no such field (amendment 2). Without one, the server sends no `Set-Cookie` and reads `Cookie` only to publish it (decision 5): nothing a
peer can do widens, so no warning is needed. Names and values are never generated (decision 6).

### 2. Scope: every HTTP reply, or those `on-path` and `on-listener` pick

`on-path=/prefix` limits the cookie to replies whose request path equals the prefix or continues it after a `/`;
`on-listener=<listen URL>` limits it to that HTTP or HTTPS listener (compared as `--fault`'s `listener` key is). Both
absent: every reply on every HTTP listener, error replies and `HEAD` included, because a header is not a body. The scoping
keys are not cookie attributes, so they never clash with `Path`. They are removed before the header is written.

### 3. Attributes written, in this order, with `; ` between each, after `name=value`

`Domain`, `Path`, `Expires`, `Max-Age`, `Secure`, `HttpOnly`, `SameSite`, each only if given; an attribute spelling is
accepted in any case and written as above. `Expires` takes an IMF-fixdate (`Wed, 09 Jun 2027 10:18:14 GMT`) and is written
as given after it validates; `Max-Age` a non-negative integer (a negative value is exit 2); `Secure` and `HttpOnly` take no
value; `SameSite` is `Strict`, `Lax` or `None`. `SameSite=None` is written even without `Secure`: whether that is
wrong is the client's to say, and a test of that is the point. No default attribute is added (no implicit `Path`, no
implicit `Secure` over TLS). A cookie is sent as its own `Set-Cookie` header line, as measured. An unknown attribute, a
repeated one, an empty name, or a name or value holding a control character, a space in the name, `;`, `,` or `=` in the
name, is exit 2 with a text naming the option and the problem.

### 4. A cookie is set as written, in the same reply, on every reply it matches

Surl keeps no per-client state (no session table), so a cookie is not set "once". A `times=N` key may be added later
without repurposing anything (configuration only grows); it is not part of this decision.

### 5. Received cookies reach the event stream and `Surl.Api` as a new event

A new record `CookieReceivedEvent(long? ExchangeId, string Name, string Value)` in `Surl.Protocol.Abstractions`, one per
`name=value` pair in each `Cookie` header of an HTTP or HTTPS request, in header order, published before the reply. It
follows ADR-0171 (overrides `ExchangeId` and `Scheme`, writes `Name` and `Value` through `WriteMembers`, value bounded to
256 bytes as event bytes are). `Surl.Api` gains `SurlEventKind.CookieReceived` and `SurlEventQuery.Name` already finds
store changes by name, so it also finds a cookie by name; `Surl.Mcp` and the control endpoint need no edit. The value is
published because a test asserts on it; the same bytes already travel in `ExchangeBytesEvent`, and a login's secret
rule (ADR-0124 decision 1) does not apply: a cookie the test set is not a credential Surl judged. The verbose log
already shows the header.

### 6. ADR-0124 decision 6 ("cookies are never seeded") still holds; no amendment

That rule keeps *generated* secrets off the seedable source. Every cookie here is a literal the test wrote, and Surl
generates none (no session identifier). If a later task generates a session cookie, it uses `RandomNumberGenerator`, and
decision 6 applies unchanged.

### 7. Library split

`Surl.Cookies.UnitLibrary` (BL-1659): `CookieDefinition` (parsed and validated spec), `CookieDefinitionReader` (the
text above), `SetCookieHeaderWriter` (the bytes of decision 3) and `CookieHeaderReader` (name and value pairs from a
`Cookie` value). It references `Surl.Protocol.Abstractions.UnitLibrary` only. The HTTP server (BL-1660) receives a
`CookieDefinition` list through its options and writes the header with `SetCookieHeaderWriter`; BL-1661 adds the option
reader, `--aihelp` facts, the `AiHelpProse` paragraph and an `AiHelpExamples` entry; BL-1662 holds the pinned-curl proof
in the measurement above (`-c`, `-b`, and `-j` against a session cookie, the last measured then).

## Consequences

FR-015 cites this ADR. A test seeds a session cookie with `--set-cookie 'session=t1;Path=/;HttpOnly'` and later asserts
`WaitForEvent(CookieReceived, name: "session")`. The Curl port is not involved.

## Amendment 1 (2026-11-24, BL-1675): the `Surl.Api` property is `SurlServerOptions.Cookies`

Decided by Claude under Stewart's delegation. Decision 1 first named the property `HttpServerOptions.Cookies`;
`Surl.Api` has no `HttpServerOptions` class. Its one options class is `SurlServerOptions`, so the property is
`SurlServerOptions.Cookies`: an `IList<string>`, empty by default, each item the text `--set-cookie` takes, composed as
that option once per item, as `Shares` and `Topics` are. A per-protocol options class would be a second way to spell one
configuration model. A null item is an `ArgumentException`; every other refusal is `surl`'s.

Also correcting decision 5's signature: the record as built (BL-1659) is
`CookieReceivedEvent(long? ExchangeId, string Scheme, string Name, string Value)`.

## Amendment 2 (2026-11-25, BL-1676): the control endpoint has no `cookies` field

Decided by Claude under Stewart's delegation. Decision 1 first named a "control endpoint start body field `cookies`".
There is no start body: `Surl.Control` drives a server that is already running (its `/v1/` routes read events and
exchanges, change faults, stores and accounts, reset and snapshot) and starts none, and the instance it drives was
configured by `--set-cookie`, a `-K` file, `Surl.Api` or `start_server`. So no field is added. Changing the cookies of a
running server is not part of this decision; if it is added later it is a new route, which only grows the surface.

`start_server`'s `cookies` (BL-1676) is an array of at most 64 strings, passed to `SurlServerOptions.Cookies`; a bad
entry is refused by `surl`'s own reader, so the tool result carries exit code 2 and the `option --set-cookie: ...` line.
