# ADR-0139 — How the MQTT and LDAP servers answer a chosen error

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-797.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4 (the fault model, the `error`
  kind and its reply table), [ADR-0014](ADR-0014-how-the-mqtt-server-answers.md) and
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md) decision 3.4 (MQTT 3.1.1),
  [ADR-0072](ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md) (LDAP),
  [ADR-0134](ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md) and
  [ADR-0135](ADR-0135-how-the-ssh-and-smb-servers-answer-a-chosen-error.md) (the same question for other
  families, whose "noted and not applied" rule this one follows).
- **Acted on by:** BL-797.

## Context

ADR-0124 decision 4 says the MQTT and LDAP servers answer a matching `error` rule at the `Command` stage; their
`command` is the packet type (`PUBLISH`, `SUBSCRIBE`) or the operation (`search`, `add`), their `path` the topic
or the base DN. An `mqtt` rule's reply is "a reason code 128 to 255 (MQTT 5) - an MQTT 3.1.1 session is
disconnected", an `ldap` rule's "a resultCode 1 to 80 other than 10 and 14, and diagnostic text", and a QoS 0
`PUBLISH`, which has no answer, ends "with a close (3.1.1)". It does not say which packets and operations ask
the plan, nor what an MQTT 3.1.1 server - whose acknowledgements carry no MQTT 5 reason code - sends for each.

## Decision

1. **MQTT 3.1.1** (`MqttPacketResponder`). The plan is asked with the listen URL's scheme (`mqtt` or `mqtts`),
   the account the login proved (none when anonymous or accepted unchecked) and the packet name:
   - `CONNECT`, once the policy has accepted the login and before `CONNACK`, with no path. A chosen error is
     `CONNACK` with the 3.1.1 return code of the same meaning as the reason code (MQTT 5.0 section 3.2.2.2:
     `0x84` 1, `0x85` 2, `0x86` 4, `0x87` 5) and 3, server unavailable, for every other, then the close
     section 3.2.2.3 requires. A refused login is answered as before and asks nothing.
   - `SUBSCRIBE`, once per valid topic filter, with the filter as sent as the path, before the bus is read.
     A chosen error is that filter's `SUBACK` return code `0x80`, 3.1.1's only failure code, whatever the
     reason code; the other filters are answered as without the rule, then ADR-0014's `DISCONNECT`.
   - `PUBLISH`, once a well-formed one is read, with its topic as the path, before the bus. 3.1.1 has no
     negative acknowledgement at any QoS, so a chosen error closes the connection with no reply and nothing
     published, retained or saved.
   `PUBREL`, `UNSUBSCRIBE`, `PINGREQ` and `DISCONNECT` change nothing and ask nothing; upstream curl sends none
   of the first three.
2. **LDAP** (`LdapSession`). The plan is asked with the scheme (`ldap` or `ldaps`), the account the last bind
   proved (`LdapBindAnswer.Account`: the simple bind's account name or the SASL step's `AccountName`; none
   when unbound, anonymous or accepted unchecked) and the operation named as RFC 4511 names it:
   - `search`, path the base DN as sent (the empty string for the root DSE), once the bind check lets it read
     (a search refused `insufficientAccessRights` asks nothing), before the directory;
   - `compare`, path the entry DN as sent, at the same point;
   - `modify`, `add`, `delete` and `modifyDN`, with no path, since the server does not decode a write it
     refuses; the chosen result replaces the read-only `unwillingToPerform`.
   A chosen error is the operation's response (`SearchResultDone` with no entry before it, `CompareResponse`
   ...) with the reply's resultCode, an empty `matchedDN` and the reply's text as the `diagnosticMessage`,
   and the connection goes on. Bind (the login, which `refuse-login` covers), `StartTLS` (no fault alters
   TLS) and other extended operations, abandon and unbind ask nothing.
3. **What fits.** MQTT reads a bare decimal 128 to 255; LDAP a resultCode 1 to 80 other than 10 (`referral`,
   which needs a URL) and 14 (`saslBindInProgress`), one space and non-empty text - `Surl.Cli`'s check
   (`FaultReplyShapes`) exactly. A reply that does not fit, which a `Surl.Api` hook could hand over unchecked,
   and a rule of any kind but `error` matched at the command stage, are noted in the exchange log and not
   applied: the packet or operation is answered as without the rule and no event is published.
4. **Events and the point.** Each application publishes one `FaultAppliedEvent` (stage `Command`, the
   exchange's ID, and as target the packet or operation name, followed by a space and the topic or DN when
   the point has one) and notes the answer. The `FaultPoint`'s listen URL is the serving engine's form, built
   by each library's own `FaultListenUrl` copy until BL-908 gives `ListenUrl` one.

## What upstream curl does with these answers

The bytes are those an existing answer already sends, which pinned upstream curl 8.21.0 has been measured
against: `SUBACK` `0x80` then `DISCONNECT` exits 8 (ADR-0102, R12); a `CONNACK` other than 0 exits 8 (BL-115
measured 4 and 5; curl 8.21.0's `mqtt.c` treats every non-zero return code alike); a QoS 0 publish waits on
nothing and exits 0 whatever follows (ADR-0102, R14), so a `PUBLISH` fault is invisible to curl's `-d` and
visible only as the event and the missing message. An LDAP search answered with a resultCode other than
`success` exits 39, as `noSuchObject` does (`UpstreamCurlSearchesSurlOverLdapTests`); curl reads
`sizeLimitExceeded` (4) as success, so a rule choosing 4 exits 0. Curl sends no compare or write. The faults'
own exchanges against a live `surl` wait on BL-803, which composes the fault plan into the running server;
BL-912 measures them then.

## Consequences

A test can make one MQTT connect, subscribe filter or publish, or one LDAP search, compare or write, fail on
purpose and see it on the event stream. No store changes. The live measurement is BL-912.
