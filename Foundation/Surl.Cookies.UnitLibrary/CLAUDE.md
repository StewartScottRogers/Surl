# Surl.Cookies.UnitLibrary

The server side of curl's cookie handling, as ADR-0189 decides it. Built here (BL-1659):

- `CookieDefinition`: one cookie to set - name, value, the attributes upstream curl parses
  (`Domain`, `Path`, `Expires`, `Max-Age`, `Secure`, `HttpOnly`, `SameSite`) and the scoping
  keys `OnPath` and `OnListener`.
- `CookieDefinitionReader.TryRead`: reads a `--set-cookie` spec into a `CookieDefinition`, or
  says what is wrong (the caller maps that to exit 2).
- `SetCookieHeaderWriter`: writes the `Set-Cookie` field in ADR-0189 decision 3's order and
  bytes (measured against curl 8.21.0).
- `CookieHeaderReader.Read`: reads a request's `Cookie` header value into `CookiePair`s.

Not here yet: the HTTP server wiring and scoping (BL-1660), the `--set-cookie` option, `-K`
and `--aihelp` (BL-1661), the `CookieReceivedEvent` (BL-1660), and any cookie jar - Surl keeps
no per-client state. This library does not read or write curl's `-c` jar files.

It references `Surl.Protocol.Abstractions.UnitLibrary` and no protocol server.
