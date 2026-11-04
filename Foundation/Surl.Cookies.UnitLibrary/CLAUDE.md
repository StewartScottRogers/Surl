# Surl.Cookies.UnitLibrary

Phase 1.

The server side of curl's cookie engine: sets cookies with every attribute upstream curl
parses (`Domain`, `Path`, `Expires`, `Max-Age`, `Secure`, `HttpOnly`, `SameSite`) from
the command line or a scripted reply, and records the `Cookie` headers curl sends back,
so every behaviour of curl's `-b`, `-c` and `-j` can be exercised against a real server.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and no protocol server.
Protocol servers receive what it provides through the contracts in Abstractions.
