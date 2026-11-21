---
id: BL-1547
title: Record in ADR-0043 that HTTP/2 and HTTP/3 sign host from :authority
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0043-http-aws-signature-version-4-check.md]
requirement: none
created: 2026-11-21
completed:
---
# BL-1547 — Record in ADR-0043 that HTTP/2 and HTTP/3 sign host from :authority

## Goal

ADR-0043 gains an amendment, marked "Decided by Claude under Stewart's delegation", that says a Signature
Version 4 request over HTTP/2 or HTTP/3 has its signed `host` header rebuilt from the `:authority`
pseudo-header, and the HTTP server hands it to the login check that way.

## Context

- Reported in the Surly cookbook's bug report of 2026-11-21 (item 1, tested against Surl `9ad60d80c`):
  `curl --aws-sigv4 aws:amz:us-east-1:s3 -u AKIAIOSFODNN7EXAMPLE:s3cret https://127.0.0.1:8443/bucket/x`
  against `surl --cert ... --key ... --user AKIAIOSFODNN7EXAMPLE:s3cret https://127.0.0.1:8443/` gets 401
  over HTTP/2 (403 with `--auth aws-sigv4`) and 200 with `--http1.1`. The client in that report was the Curl
  port, which is never evidence (ADR-0003); the evidence below is upstream curl's own source.
- Upstream curl 8.21.0 (tag `curl-8_21_0`), read for this task:
  - `lib/http_aws_sigv4.c`, `make_headers` (about line 401): when the user set no `Host` header, the signed
    header list starts with `data->state.aptr.host` (the `Host: <host>[:port]` line curl builds from the
    URL) cut at the CR, else `host:<hostname>`. So `host` is always signed, whatever the HTTP version.
  - `lib/http.c`, `Curl_http_req_to_h2` (about line 4923) takes `:authority` from the request's `Host`
    header, and `h2_permissible_field` (about line 4883, the `H2_NON_FIELD` list) drops `Host` from the
    HTTP/2 and HTTP/3 field list. So the server receives `:authority: 127.0.0.1:8443` and no `host` field,
    while the signature covers `host:127.0.0.1:8443`.
- RFC 9113 section 8.3.1 and RFC 9114 section 4.3.1: `:authority` carries what `Host` carries in HTTP/1.1,
  and a gateway making an HTTP/1.1 request from it builds `Host` from `:authority`.
- Surl today: `Protocols/Http/Surl.Http2.UnitLibrary/Http2RequestHeadMapping.cs:49-52` keeps only the regular
  fields in `Fields` and puts `:authority` on `Http2RequestHead.Authority` (`Authority`, lines 133-143);
  `Surl.Http3`'s `Http3RequestHeadMapping` does the same. `HttpRequestAnswerer.AuthenticationRequest`
  (`Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.cs:208-212`) passes `request.Fields`
  only, so `AwsSigV4CanonicalRequest.BuildHeaders`
  (`Foundation/Surl.Authentication.UnitLibrary/AwsSigV4CanonicalRequest.cs:90-109`) finds no `host` field
  and returns `null`, and the request is refused. Every HTTP/2 and HTTP/3 Signature Version 4 login fails.
- The S3 and Cloud Storage servers answer HTTP/1.1 only (ADR-0108 decision 5, ADR-0110), so only the HTTP
  server is affected. BL-1548 makes the change; this task records it first.
- Stand-in: no store, event or fault changes. A test using `https://` with `--aws-sigv4` gets the same
  answer over HTTP/1.1, HTTP/2 and HTTP/3.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0043-http-aws-signature-version-4-check.md` has an amendment
  stating: over HTTP/2 and HTTP/3, when the request has no `host` field, the HTTP server hands the login
  check a `host` field whose value is `:authority` as received; a request that also carries a `host` field
  is judged on the fields as received.
- [ ] The amendment cites `lib/http_aws_sigv4.c` `make_headers` and `lib/http.c` `Curl_http_req_to_h2` and
  `h2_permissible_field` at `curl-8_21_0`, and RFC 9113 section 8.3.1, as its evidence, and names no Curl
  port output.
- [ ] The amendment says the change lives in the HTTP server (`HttpRequestAnswerer`), not in
  `Surl.Authentication` and not in the shared `HttpAuthenticationRequest` contract, and why (the S3 and
  Cloud Storage servers answer HTTP/1.1 only).
- [ ] The ADR's header lists the amendment and BL-1548 as the task that builds it.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
