# NATS requirement matrix

The server-side requirements `Surl.Extended.Protocol.Nats` answers, for the `nats` and `natss`
schemes (ADR-0083 decision 9). Written from the NATS client protocol page
(<https://docs.nats.io/reference/reference-protocols/nats-protocol>), the "Subject-Based Messaging"
page (<https://docs.nats.io/nats-concepts/subjects>), the "Queue Groups" page
(<https://docs.nats.io/nats-concepts/core-nats/queue>) and NATS ADR-4 "NATS Message Headers", read
2026-11-05, the specifications
[ADR-0113](../../Planning/Decisions/ADR-0113-how-the-nats-server-answers-nats-and-natss.md) decides
by, never from the Curl port's client matrix. Each row binds the server; "Covered by" names the task
that proves it, until that task replaces its ID with the named test, or says why the row needs no
test.

The pages state few requirements in RFC keywords, so each field a server sends, each operation it
reads and each `-ERR` it owes is a row. IDs are `Nats:NATS-<operation>-<n>` for the protocol page,
`Nats:Subject-<n>` for the subjects page, `Nats:Queue-<n>` for the queue-groups page,
`Nats:Headers-<n>` for NATS ADR-4, and `Nats:Surl-<Decision>-<n>` for a rule ADR-0113 adds where
the pages leave the server a choice.

## Control lines (protocol page, "Protocol conventions")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Nats:NATS-PROTO-1 | MUST | Operation names are matched case-insensitively (`ping` is answered `PONG`). | BL-525 |
| Nats:NATS-PROTO-2 | MUST | Fields are separated by spaces or tabs; a control line ends with CR LF (a bare LF accepted). | BL-525 |
| Nats:NATS-PROTO-3 | MUST | A control line is read however its bytes are split across reads, and pipelined operations are each answered in order. | BL-525 |
| Nats:NATS-PROTO-4 | MUST | A payload is read by its byte count, CR LF inside it included, and must be followed by CR LF, else `-ERR 'Parser Error'`, then close. | `NatsControlLineReaderTests.ReadPayloadAsync_PayloadSplitAcrossReadsWithCrLfInside_IsReadByItsCount`, `NatsControlLineReaderTests.ReadPayloadAsync_PayloadNotFollowedByCrLf_IsParserError`, `NatsServerConnectionTests.ServeAsync_MalformedArgumentsOrPayloadEnd_IsParserErrorAndClosed` |
| Nats:NATS-ERR-1 | MUST | An unknown operation, or INFO, MSG or HMSG from a client, is `-ERR 'Unknown Protocol Operation'`, then close. | BL-525 |
| Nats:NATS-ERR-2 | MUST | Malformed arguments to a known operation are `-ERR 'Parser Error'`, then close. | BL-525 |
| Nats:NATS-ERR-3 | MUST | A control line over `--max-line` is `-ERR 'Maximum Control Line Exceeded'`, then close. | BL-525 |
| Nats:NATS-ERR-4 | MUST NOT | No `-ERR` names an account, a path, an exception or a version; only the client's own subject is echoed. | BL-525 |
| Nats:NATS-ERR-5 | MUST NOT | `'Attempted To Connect To Route Port'` is never sent. | n/a: there is no route port, so no code path can send it |

## INFO (protocol page, "INFO")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Nats:NATS-INFO-1 | MUST | INFO is the first line of every connection (on `natss`, as Nats:NATS-TLS-1 and -2 say). | BL-525 |
| Nats:NATS-INFO-2 | MUST | INFO carries `server_id`, `server_name`, `version`, `proto`, `host`, `port`, `headers`, `max_payload`, `client_id`, `client_ip`, `auth_required`, `tls_required`, `tls_verify` and `tls_available`, in that order, as ADR-0113 decision 1 gives them. | BL-525 |
| Nats:NATS-INFO-3 | MUST NOT | `version` is never surl's version (ADR-0006 section 3); it is `2.10.0`. | BL-525 |
| Nats:NATS-INFO-4 | MUST NOT | `connect_urls`, `ws_connect_urls`, `nonce`, `jetstream`, `go` and `git_commit` are not sent. | BL-525 |
| Nats:NATS-INFO-5 | MUST NOT | INFO is never sent again after the first. | BL-525 |

## CONNECT and authentication (protocol page, "CONNECT")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Nats:NATS-CONNECT-1 | MUST | `user` and `pass` log in as that account; else `auth_token` as the empty-name account whose password it is; judged by `IAuthenticationPolicy`. | BL-525 |
| Nats:NATS-CONNECT-2 | MUST | Without `--allow-anonymous`, CONNECT without credentials, with refused credentials, or refused as plain text on `nats` is `-ERR 'Authorization Violation'` after the refusal delay, then close. | BL-525 |
| Nats:NATS-CONNECT-3 | MUST | Under `--allow-anonymous`, INFO says `auth_required: false` and CONNECT without credentials is served. | BL-525 |
| Nats:NATS-CONNECT-4 | MUST | `nkey`, `sig` or `jwt` in CONNECT is `-ERR 'Authorization Violation'`, then close. | BL-525 |
| Nats:NATS-CONNECT-5 | MUST | An operation before CONNECT is `-ERR 'Authorization Violation'`, then close. | BL-525 |
| Nats:NATS-CONNECT-6 | MUST | No complete CONNECT by `--head-timeout` is `-ERR 'Authorization Timeout'`, then close. | BL-525 |
| Nats:NATS-CONNECT-7 | MUST | CONNECT whose argument is not a JSON object, or has a field of the wrong type, is `-ERR 'Parser Error'`, then close; unknown fields are ignored. | BL-525 |
| Nats:NATS-CONNECT-8 | MUST | `protocol` other than 0 or 1 is `-ERR 'Invalid Client Protocol'`, then close. | BL-525 |
| Nats:NATS-CONNECT-9 | MUST | `verbose: true` answers each accepted CONNECT, PUB, HPUB, SUB and UNSUB `+OK`, and never PING or PONG. | BL-525 |
| Nats:NATS-CONNECT-10 | MUST | `no_responders: true` without `headers: true` is `-ERR 'No Responders Requires Headers Support'`, then close. | BL-525 |
| Nats:NATS-CONNECT-11 | MUST | A second CONNECT is `-ERR 'Unknown Protocol Operation'`, then close. | BL-525 |
| Nats:Surl-Connect-1 | MAY | `pedantic`, `tls_required`, `name`, `lang` and `version` are accepted and change nothing. | BL-525 |

## Keep-alive (protocol page, "PING/PONG")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Nats:NATS-PING-1 | MUST | A client's PING is answered PONG. | BL-525 |
| Nats:NATS-PING-2 | MUST | The server sends PING every quarter of `--idle-timeout` (120 s when it is off) from CONNECT on. | BL-525 |
| Nats:NATS-PING-3 | MUST | A client's PONG clears the unanswered count; at an interval with 2 unanswered, the server sends `-ERR 'Stale Connection'`, then closes. | BL-525 |

## Publishing and subscribing (protocol page, PUB, HPUB, SUB, UNSUB, MSG, HMSG; subjects page)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Nats:NATS-PUB-1 | MUST | PUB is delivered as MSG to every matching subscription, payload exact, with the reply-to when given. | `NatsServerConnectionTests.ServeAsync_PubWithTwoSubscribers_IsDeliveredAsMsgToEach` |
| Nats:NATS-PUB-2 | MUST | A payload (HPUB: the total) over `max_payload` is `-ERR 'Maximum Payload Violation'`, then close, before the payload is read. | `NatsServerConnectionTests.ServeAsync_PayloadOverMaxPayload_IsMaximumPayloadViolationAndClosed`, `NatsServerConnectionTests.ServeAsync_HpubTotalOverMaxPayload_IsMaximumPayloadViolationAndClosed` |
| Nats:NATS-SUB-1 | MUST | SUB names a subject, an optional queue group and a `sid`; a SUB reusing a live `sid` is ignored. | `NatsServerConnectionTests.ServeAsync_SubReusingALiveSid_IsIgnored`, `NatsServerConnectionTests.ServeAsync_QueueGroupOfTwo_GivesEachMemberOneOfTwoMessages` |
| Nats:NATS-UNSUB-1 | MUST | UNSUB without `max_msgs` ends the subscription at once; an unknown `sid` is ignored. | `NatsServerConnectionTests.ServeAsync_UnsubWithoutMaxAndUnknownSidUnderVerbose_EndsAtOnceAndAnswersOk` |
| Nats:NATS-UNSUB-2 | MUST | UNSUB with `max_msgs` ends the subscription after that many deliveries in all (at once if already reached). | `NatsServerConnectionTests.ServeAsync_UnsubWithMaxTwo_EndsAfterTwoMessages`, `NatsServerConnectionTests.ServeAsync_UnsubWithMaxAlreadyReached_EndsAtOnceAndTheSidMayBeReused` |
| Nats:NATS-MSG-1 | MUST | MSG and HMSG carry the subject, `sid`, optional reply-to and sizes as the page's syntax gives them. | `NatsServerConnectionTests.ServeAsync_PubWithTwoSubscribers_IsDeliveredAsMsgToEach`, `NatsServerConnectionTests.ServeAsync_Hpub_IsDeliveredAsHmsgWithItsHeaderBlockByteForByte` |
| Nats:NATS-ECHO-1 | MUST | `echo: false` suppresses delivery of a connection's own publishes to its own subscriptions. | `NatsServerConnectionTests.ServeAsync_EchoFalse_SuppressesSelfDeliveryButNotOthers` |
| Nats:Subject-1 | MUST | `*` matches exactly one token and `>` (last only) one or more. | `NatsSubjectsTests.Matches_SubscriptionAndSubject_IsWhatTheSubjectsPageSays`, `NatsServerConnectionTests.ServeAsync_StarAndFullWildcards_MatchOneTokenAndOneOrMore` |
| Nats:Subject-2 | MUST | An invalid SUB subject is `-ERR 'Invalid Subject'`, the connection kept. | `NatsServerConnectionTests.ServeAsync_InvalidSubSubject_IsInvalidSubjectAndKept` |
| Nats:Subject-3 | MUST | A PUB or HPUB subject or reply-to with a wildcard, an empty token or an unmappable token is `-ERR 'Invalid Publish Subject'`, the connection kept. | `NatsServerConnectionTests.ServeAsync_InvalidPublishSubjectOrReplyTo_IsInvalidPublishSubjectAndKept` |
| Nats:Queue-1 | MUST | Among a queue group's subscriptions on one subject, each message goes to exactly one member. | `NatsServerConnectionTests.ServeAsync_QueueGroupOfTwo_GivesEachMemberOneOfTwoMessages` |
| Nats:Surl-Bus-1 | MUST | Subjects map onto the shared bus (ADR-0100 decision 3.2): a message published over `mqtt` reaches a NATS subscriber on the mapped subject, and the reverse; a bus topic NATS cannot name is never delivered to NATS. | `NatsServerConnectionTests.ServeAsync_MessagePublishedUnderAnMqttName_IsDeliveredAsMsgOnTheMappedSubject`, `NatsServerConnectionTests.ServeAsync_Pub_ReachesAnMqttSubscriberOnTheMappedTopic` |
| Nats:Surl-Bus-2 | MUST | A SUB or PUB denied by `--topic-access` is `-ERR 'Permissions Violation for Subscription to <subject>'` / `'Permissions Violation for Publish to <subject>'`, the connection kept. | `NatsServerConnectionTests.ServeAsync_PubAndSubRefusedByTopicAccess_AreAnsweredPermissionsViolationAndKept` |
| Nats:Surl-Bus-3 | MUST | Past the bus's 1000 subscriptions a connection, SUB is `-ERR 'Maximum Subscriptions Exceeded'`, the connection kept; a full bus store is `-ERR 'Resource Limit Exceeded'`, then close. | `NatsServerConnectionTests.ServeAsync_SubPastTheBusBound_IsMaximumSubscriptionsExceededAndKept`, `NatsServerConnectionTests.ServeAsync_PubPastTheBusStore_IsResourceLimitExceededAndClosed` |

## Headers and no responders (protocol page, HPUB, HMSG; NATS ADR-4)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Nats:Headers-1 | MUST | HPUB is accepted only after CONNECT set `headers: true`; otherwise `-ERR 'Unknown Protocol Operation'`, then close. | `NatsServerConnectionTests.ServeAsync_HpubWithoutHeaders_IsUnknownProtocolOperationAndClosed` |
| Nats:Headers-2 | MUST | An HPUB's header block reaches a NATS subscriber that set `headers` byte for byte, as HMSG. | `NatsServerConnectionTests.ServeAsync_Hpub_IsDeliveredAsHmsgWithItsHeaderBlockByteForByte` |
| Nats:Headers-3 | MUST | A message with headers reaches a subscriber without `headers` as MSG with its payload only. | `NatsServerConnectionTests.ServeAsync_Hpub_IsDeliveredAsHmsgWithItsHeaderBlockByteForByte`, `NatsServerConnectionTests.ServeAsync_HpubWithAnEmptyHeaderBlock_IsDeliveredAsMsg` |
| Nats:Headers-4 | MUST | Headers from another protocol reach a NATS subscriber as a `NATS/1.0` block of `name: value` lines, names case-preserved. | `NatsServerConnectionTests.ServeAsync_HeadersFromAnotherProtocol_ReachAHeadersSubscriberAsANatsBlock`, `NatsHeaderBlockTests.Encode_HeadersItCanCarry_WritesTheBlockInOrderLeavingOutTheRest` |
| Nats:Headers-5 | MUST | A request delivered to no subscription, from a connection with `no_responders` and `headers`, gets `HMSG <reply-to> <sid> 16 16` with the block `NATS/1.0 503` on each of its own subscriptions matching the reply-to. | `NatsServerConnectionTests.ServeAsync_RequestWithNoSubscriberUnderNoResponders_IsAnswered`, `NatsServerConnectionTests.ServeAsync_NoResponders_NeedsHeaders` |
| Nats:Surl-Limits-1 | MUST | A connection with 64 MiB unsent is cut off with `-ERR 'Slow Consumer'`, then closed. | `NatsServerConnectionTests.ServeAsync_SlowConsumer_IsCutOffWithSlowConsumer` |

## TLS (protocol page, INFO `tls_required`)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Nats:NATS-TLS-1 | MUST | On `natss`, a client sending nothing within 50 ms gets INFO with `tls_required: true` in plain text; a following `0x16` byte starts the TLS handshake, and CONNECT over TLS is accepted. | `NatsServerConnectionTests.ServeAsync_NatssInfoFirst_SendsInfoWithTlsRequiredThenUpgradesAndAcceptsConnectOverTls` |
| Nats:NATS-TLS-2 | MUST | On `natss`, a client sending its first byte within 50 ms gets the TLS handshake first and INFO inside TLS. | `NatsServerConnectionTests.ServeAsync_NatssTlsFirst_UpgradesBeforeSendingInfoInsideTls`, `NatsServerConnectionTests.ServeAsync_NatssAnyByteWithinTheTlsFirstWait_StartsTheHandshakeBeforeInfo` |
| Nats:NATS-TLS-3 | MUST | On `natss`, plain text after INFO with `tls_required` is `-ERR 'Secure Connection - TLS Required'`, then close. | `NatsServerConnectionTests.ServeAsync_NatssPlainConnectAfterInfo_IsSecureConnectionTlsRequiredAndClosed` |
| Nats:NATS-TLS-4 | MUST NOT | `nats` never offers TLS: `tls_required` and `tls_available` are `false`, with or without a certificate. | `NatsServerConnectionTests.ServeAsync_NatsWithAClientHello_NeverOffersOrStartsTls`, `NatsInfoTests.Encode_IPv4_IsTheInfoLineWithTheAdrsFieldsInOrder` |
| Nats:NATS-TLS-5 | MUST | A failed TLS handshake closes with no bytes. | `NatsServerConnectionTests.ServeAsync_NatssHandshakeFails_ThrowsWithNoBytesWritten`, `NatsServerConnectionTests.ServeAsync_NatssHandshakeUnfinishedWithinTheHeadTimeout_ClosesWithNoBytes` |

## Connections (ADR-0006)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Nats:Surl-Limits-2 | MUST | A connection past `--max-connections` or `--max-connections-per-address` gets `-ERR 'Maximum Connections Exceeded'`, then close, with no INFO. | BL-525 |
