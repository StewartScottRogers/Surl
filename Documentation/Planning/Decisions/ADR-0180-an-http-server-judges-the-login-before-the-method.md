# ADR-0180 — An HTTP server judges the login before the method, so POST, DELETE and PATCH are 401 unless `--allow-anonymous` is given

- **Status:** Accepted
- **Date:** 2026-11-19
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-19, in
  BL-1393. No package, no threshold change, no download.
- **Clarifies:** [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 4,
  whose order the server already follows; the `--aihelp http` paragraph did not say it.

## Context

Surly cookbook bug report 2026-11-19, finding 1: with no `--user` and no `--allow-anonymous`,
`curl -X POST -d x http://127.0.0.1:8080/hello.txt` and `-X DELETE` were answered `401 Unauthorized` with Digest
challenges, while `surl --aihelp http` said POST, PUT, DELETE, CONNECT, OPTIONS, TRACE and PATCH are answered
`405 Method Not Allowed`.

The server asks its authentication session about every request before it looks at the method
(`HttpRequestAnswerer`), and the session treats every method but GET and HEAD as a write
(`HttpAuthenticationRequest.IsWrite`). Without accounts only a read is let in anonymously (ADR-0032 section 4).

## Decision

1. **The login check comes before the method check**, always. A peer that may not log in learns nothing of which
   methods the server serves: not the `Allow` field, not that a method is unknown.
2. **No account and no `--allow-anonymous`:** GET and HEAD are served; every other method, known or not, is
   answered `401 Unauthorized` with the Digest challenges, and a body it announced is not read.
3. **An account exists:** every request, GET and HEAD too, needs a login. A peer that logs in is then answered by
   the method check: 405 with `Allow: GET, HEAD` (`GET, HEAD, PUT` under `--allow-uploads`), 501 for an unknown
   method.
   **Changed by
   [ADR-0132 amendment 5](ADR-0132-how-the-account-book-changes-while-the-server-runs.md#amendment-5-2026-11-27-bl-1854-removing-the-last-account-does-not-open-the-server)**
   (2026-11-27), for decisions 2 and 3: after the last account is removed from a scheme that has had one to log in
   to, a `GET` or `HEAD` without credentials is not served; it is `401` or `403`.
4. **`--allow-anonymous`:** every request is let in, so the method check answers alone: POST, DELETE, PATCH and
   the other known methods 405 with `Allow: GET, HEAD` (`GET, HEAD, PUT` under `--allow-uploads`, which also
   serves PUT), an unknown method 501.
5. **The server is unchanged; the `--aihelp http` paragraph says this order**, and the tests pin it.

## Consequences

- The 405 text in `--aihelp http` is now conditional on a login being possible. The cookbook recipe
  `http-serve-an-empty-store` is right to say `401` for DELETE.
- Nothing about upstream curl's bytes changes: the 401 and 405 answers are the ones already pinned.
